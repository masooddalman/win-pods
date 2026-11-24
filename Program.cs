using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;

namespace WinPods
{
    public static class Program
    {
        const string AppName = "WinPods";
        const int NearRssi = -65;   // ignore AirPods that are not close to this PC

        static System.Windows.Application _app;
        static PopupWindow _popup;
        static TrayIcon _tray;
        static readonly BatteryInfo _battery = new BatteryInfo();
        static string _name = "AirPods Pro";
        static bool _connected;
        static DateTime _lastShown = DateTime.MinValue;
        static readonly List<object> _keepAlive = new List<object>();
        static int _lastLid = -1;
        static bool _caseSeen;
        static string _logPath;   // set by --log: raw AirPods packets, for debugging

        static void Log(string line)
        {
            try { System.IO.File.AppendAllText(_logPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + line + "\r\n"); }
            catch { }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            // --log: debug mode. Writes every Apple packet and every popup decision to %TEMP%\winpods-ble.log.
            // It runs beside a normal instance and never shows a popup.
            bool logMode = Array.IndexOf(args, "--log") >= 0;
            if (logMode) _logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "winpods-ble.log");

            using var mutex = new Mutex(true, "Local\\" + AppName + (logMode ? "-log" : Array.IndexOf(args, "--demo") >= 0 ? "-demo" : ""), out bool first);
            if (!first) return;

            _app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            _popup = new PopupWindow();
            BuildTray();

            bool demo = Array.IndexOf(args, "--demo") >= 0;
            if (demo)
            {
                _battery.Left = 80; _battery.Right = 70; _battery.Case = 55; _battery.RightCharging = true;
                _popup.SetBattery(_battery);
                if (Array.IndexOf(args, "--demo-off") >= 0) _popup.ShowDisconnected();
                _popup.Present();
            }
            else
            {
                StartBluetoothWatcher();
                StartAdvertisementWatcher();
            }
            _app.Run();
        }

        // ---------- Connection detection ----------

        static void StartBluetoothWatcher()
        {
            var watcher = DeviceInformation.CreateWatcher(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
            watcher.Added += async (w, info) =>
            {
                if (info.Name != null && info.Name.IndexOf("AirPods", StringComparison.OrdinalIgnoreCase) >= 0)
                    await TrackDevice(info.Id, info.Name);
            };
            watcher.Start();
            _keepAlive.Add(watcher);
        }

        static async Task TrackDevice(string id, string name)
        {
            BluetoothDevice dev = await BluetoothDevice.FromIdAsync(id);
            if (dev == null) return;
            _keepAlive.Add(dev);
            _name = name;

            dev.ConnectionStatusChanged += (d, o) =>
                Dispatch(() => OnConnectionChanged(d.Name, d.ConnectionStatus == BluetoothConnectionStatus.Connected, true));

            // Already connected when the app starts: show the tray state, no popup.
            if (dev.ConnectionStatus == BluetoothConnectionStatus.Connected)
                Dispatch(() => OnConnectionChanged(dev.Name, true, false));
        }

        static void OnConnectionChanged(string name, bool connected, bool popup)
        {
            bool wasConnected = _connected;
            _connected = connected;
            if (!string.IsNullOrWhiteSpace(name)) _name = name;
            if (connected)
            {
                _popup.SetName(_name);
                _popup.SetBattery(_battery);
                _popup.ShowConnected();
                if (popup && _logPath == null && (DateTime.Now - _lastShown).TotalSeconds > 4)
                {
                    _lastShown = DateTime.Now;
                    _popup.Present();
                }
            }
            else
            {
                _battery.Left = _battery.Right = _battery.Case = -1;
                _battery.LeftCharging = _battery.RightCharging = _battery.CaseCharging = false;
                // Tell the user the AirPods are gone, but only if we knew they were connected.
                if (wasConnected && popup && _logPath == null)
                {
                    _popup.SetName(_name);
                    _popup.ShowDisconnected();
                    _popup.Present();
                }
            }
            UpdateTray();
        }

        // ---------- Battery from BLE advertisements ----------

        static void StartAdvertisementWatcher()
        {
            var w = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
            var filter = new BluetoothLEAdvertisement();
            filter.ManufacturerData.Add(new BluetoothLEManufacturerData { CompanyId = 0x004C });
            w.AdvertisementFilter.Advertisement = filter;
            w.Received += (s, e) =>
            {
                short rssi = e.RawSignalStrengthInDBm;
                foreach (var m in e.Advertisement.ManufacturerData)
                {
                    if (m.CompanyId != 0x004C) continue;
                    byte[] data = m.Data.ToArray();
                    if (_logPath != null) Log("addr=" + e.BluetoothAddress.ToString("X12") + " rssi=" + rssi + " connected=" + _connected + " " + BitConverter.ToString(data));
                    if (data.Length == 0 || data[0] != 0x07) continue;
                    if (rssi < NearRssi) continue;
                    var parsed = new BatteryInfo { Left = _battery.Left, Right = _battery.Right, Case = _battery.Case,
                        LeftCharging = _battery.LeftCharging, RightCharging = _battery.RightCharging, CaseCharging = _battery.CaseCharging };
                    if (!BatteryInfo.TryParse(data, parsed)) continue;
                    Dispatch(() =>
                    {
                        _battery.Left = parsed.Left; _battery.Right = parsed.Right; _battery.Case = parsed.Case;
                        _battery.LeftCharging = parsed.LeftCharging; _battery.RightCharging = parsed.RightCharging; _battery.CaseCharging = parsed.CaseCharging;
                        _popup.SetBattery(_battery);
                        UpdateTray();

                        // Lid opened: the lid counter changed, or the case battery became visible again.
                        bool lidChanged = parsed.Lid >= 0 && _lastLid >= 0 && parsed.Lid != _lastLid;
                        bool caseAppeared = parsed.CaseInPacket && !_caseSeen;
                        if (parsed.Lid >= 0) _lastLid = parsed.Lid;
                        _caseSeen = parsed.CaseInPacket;

                        if (!_popup.IsVisible && _connected && parsed.CaseInPacket && (lidChanged || caseAppeared)
                                 && (DateTime.Now - _lastShown).TotalSeconds > 3)
                        {
                            _lastShown = DateTime.Now;
                            if (_logPath != null) { Log("TRIGGER lid-open popup"); return; }
                            _popup.SetName(_name);
                            _popup.ShowConnected();
                            _popup.Present();
                        }
                    });
                }
            };
            w.Start();
            _keepAlive.Add(w);
        }

        // ---------- Tray icon ----------

        static void BuildTray()
        {
            _tray = new TrayIcon
            {
                Text = "AirPods: not connected",
                IsAutoStart = IsAutoStart,
                SetAutoStart = SetAutoStart,
                ExitRequested = () => { _tray.Dispose(); _app.Shutdown(); }
            };
            _tray.Show();
        }

        static void UpdateTray()
        {
            string t = _connected ? _name + "  " + _battery.Summary() : "AirPods: not connected";
            _tray.Text = t;
        }

        // ---------- Start with Windows ----------

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        static bool IsAutoStart()
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(AppName) != null;
        }
        static void SetAutoStart(bool on)
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (on) k.SetValue(AppName, "\"" + Environment.ProcessPath + "\"");
            else k.DeleteValue(AppName, false);
        }

        static void Dispatch(Action a) => _app.Dispatcher.BeginInvoke(a);
    }
}
