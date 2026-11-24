using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinPods
{
    /// <summary>
    /// Notification-area icon with a right-click menu, using plain Win32 calls.
    /// Replaces System.Windows.Forms.NotifyIcon so the app does not ship Windows Forms.
    /// </summary>
    public sealed class TrayIcon : IDisposable
    {
        const int WM_APP_TRAY = 0x8000 + 1;
        const int WM_RBUTTONUP = 0x0205;
        const int WM_CONTEXTMENU = 0x007B;
        const int NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
        const int NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;
        const uint MF_STRING = 0, MF_CHECKED = 8, MF_SEPARATOR = 0x800;
        const uint TPM_RETURNCMD = 0x100, TPM_RIGHTBUTTON = 2, TPM_BOTTOMALIGN = 0x20;
        static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        const int CmdAutoStart = 1, CmdExit = 2;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NOTIFYICONDATA
        {
            public int cbSize; public IntPtr hWnd; public int uID; public int uFlags; public int uCallbackMessage; public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public int dwState, dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public int dwInfoFlags; public Guid guidItem; public IntPtr hBalloonIcon;
        }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIcon(int msg, ref NOTIFYICONDATA data);
        [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string text);
        [DllImport("user32.dll")] static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hWnd, IntPtr rect);
        [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] static extern IntPtr CreateIconFromResourceEx(byte[] bits, int size, bool icon, int version, int cx, int cy, int flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int RegisterWindowMessage(string name);

        readonly HwndSource _window;
        readonly IntPtr _icon;
        readonly int _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        string _tip = "";
        bool _added;

        public Func<bool> IsAutoStart;
        public Action<bool> SetAutoStart;
        public Action ExitRequested;

        public TrayIcon()
        {
            _window = new HwndSource(new HwndSourceParameters("WinPodsTray") { ParentWindow = HWND_MESSAGE, Width = 0, Height = 0 });
            _window.AddHook(WndProc);
            _icon = BuildIcon();
        }

        public string Text
        {
            get => _tip;
            set { _tip = value.Length > 127 ? value.Substring(0, 127) : value; if (_added) Notify(NIM_MODIFY); }
        }

        public void Show() { Notify(NIM_ADD); _added = true; }

        void Notify(int msg)
        {
            var d = new NOTIFYICONDATA
            {
                hWnd = _window.Handle, uID = 1, uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_APP_TRAY, hIcon = _icon, szTip = _tip, szInfo = "", szInfoTitle = ""
            };
            d.cbSize = Marshal.SizeOf(d);
            Shell_NotifyIcon(msg, ref d);
        }

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_APP_TRAY && (lParam.ToInt32() == WM_RBUTTONUP || lParam.ToInt32() == WM_CONTEXTMENU))
            {
                ShowMenu();
                handled = true;
            }
            else if (msg == _taskbarCreated && _added)
            {
                Notify(NIM_ADD);   // Explorer restarted: add the icon again.
            }
            return IntPtr.Zero;
        }

        void ShowMenu()
        {
            IntPtr menu = CreatePopupMenu();
            bool auto = IsAutoStart != null && IsAutoStart();
            AppendMenu(menu, MF_STRING | (auto ? MF_CHECKED : 0), (UIntPtr)CmdAutoStart, "Start with Windows");
            AppendMenu(menu, MF_SEPARATOR, UIntPtr.Zero, null);
            AppendMenu(menu, MF_STRING, (UIntPtr)CmdExit, "Exit");
            GetCursorPos(out POINT p);
            SetForegroundWindow(_window.Handle);   // lets the menu close when the user clicks elsewhere
            int cmd = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN, p.X, p.Y, 0, _window.Handle, IntPtr.Zero);
            DestroyMenu(menu);

            if (cmd == CmdAutoStart) SetAutoStart?.Invoke(!auto);
            else if (cmd == CmdExit) ExitRequested?.Invoke();
        }

        /// <summary>Draws a small earbud shape with WPF and turns it into an icon (PNG data, no System.Drawing).</summary>
        static IntPtr BuildIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawEllipse(Brushes.White, null, new Point(14, 10), 8, 8);
                dc.DrawRoundedRectangle(Brushes.White, null, new Rect(15, 12, 5, 17), 2.5, 2.5);
            }
            var bmp = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var ms = new MemoryStream();
            enc.Save(ms);
            byte[] png = ms.ToArray();
            return CreateIconFromResourceEx(png, png.Length, true, 0x00030000, 32, 32, 0);
        }

        public void Dispose()
        {
            if (_added) { Notify(NIM_DELETE); _added = false; }
            _window.Dispose();
            if (_icon != IntPtr.Zero) DestroyIcon(_icon);
        }
    }
}
