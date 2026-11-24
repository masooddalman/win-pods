using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using WinForms = System.Windows.Forms;

namespace WinPods
{
    public static class Program
    {
        const string AppName = "WinPods";
        const int NearRssi = -65;   // ignore AirPods that are not close to this PC

        static System.Windows.Application _app;
        static PopupWindow _popup;
        static WinForms.NotifyIcon _tray;
        static readonly BatteryInfo _battery = new BatteryInfo();
        static string _name = "AirPods Pro";
        static bool _connected;
        static DateTime _lastShown = DateTime.MinValue;
        static readonly List<object> _keepAlive = new List<object>();

        [STAThread]
        public static void Main(string[] args)
        {
            using var mutex = new Mutex(true, "Local\\" + AppName, out bool first);
            if (!first) return;

            _app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            _popup = new PopupWindow();
            BuildTray();

            bool demo = Array.IndexOf(args, "--demo") >= 0;
            if (demo)
            {
                _battery.Left = 80; _battery.Right = 70; _battery.Case = 55; _battery.RightCharging = true;
                _popup.SetBattery(_battery);
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
            _connected = connected;
            if (!string.IsNullOrWhiteSpace(name)) _name = name;
            if (connected)
            {
                _popup.SetName(_name);
                _popup.SetBattery(_battery);
                if (popup && (DateTime.Now - _lastShown).TotalSeconds > 4)
                {
                    _lastShown = DateTime.Now;
                    _popup.Present();
                }
            }
            else
            {
                _popup.Dismiss();
                _battery.Left = _battery.Right = _battery.Case = -1;
                _battery.LeftCharging = _battery.RightCharging = _battery.CaseCharging = false;
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
                if (e.RawSignalStrengthInDBm < NearRssi) return;
                foreach (var m in e.Advertisement.ManufacturerData)
                {
                    if (m.CompanyId != 0x004C) continue;
                    byte[] data = m.Data.ToArray();
                    var parsed = new BatteryInfo { Left = _battery.Left, Right = _battery.Right, Case = _battery.Case,
                        LeftCharging = _battery.LeftCharging, RightCharging = _battery.RightCharging, CaseCharging = _battery.CaseCharging };
                    if (!BatteryInfo.TryParse(data, parsed)) continue;
                    Dispatch(() =>
                    {
                        _battery.Left = parsed.Left; _battery.Right = parsed.Right; _battery.Case = parsed.Case;
                        _battery.LeftCharging = parsed.LeftCharging; _battery.RightCharging = parsed.RightCharging; _battery.CaseCharging = parsed.CaseCharging;
                        _popup.SetBattery(_battery);
                        if (_popup.IsVisible) _popup.ExtendLife();
                        UpdateTray();
                    });
                }
            };
            w.Start();
            _keepAlive.Add(w);
        }

        // ---------- Tray icon ----------

        static void BuildTray()
        {
            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("Show now", null, (s, e) => { _popup.SetName(_name); _popup.SetBattery(_battery); _popup.Present(); });
            var auto = new WinForms.ToolStripMenuItem("Start with Windows") { Checked = IsAutoStart(), CheckOnClick = true };
            auto.CheckedChanged += (s, e) => SetAutoStart(auto.Checked);
            menu.Items.Add(auto);
            menu.Items.Add("Exit", null, (s, e) => { _tray.Visible = false; _tray.Dispose(); _app.Shutdown(); });

            _tray = new WinForms.NotifyIcon { Icon = MakeIcon(), Visible = true, ContextMenuStrip = menu, Text = "AirPods: not connected" };
            _tray.MouseClick += (s, e) =>
            {
                if (e.Button == WinForms.MouseButtons.Left) { _popup.SetName(_name); _popup.SetBattery(_battery); _popup.Present(); }
            };
        }

        static void UpdateTray()
        {
            string t = _connected ? _name + "  " + _battery.Summary() : "AirPods: not connected";
            _tray.Text = t.Length > 63 ? t.Substring(0, 63) : t;
        }

        static Icon MakeIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                g.FillEllipse(Brushes.White, 6, 2, 16, 16);
                g.FillRectangle(Brushes.White, 15, 12, 5, 17);
                g.FillEllipse(Brushes.White, 15, 25, 5, 5);
            }
            return Icon.FromHandle(bmp.GetHicon());
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
