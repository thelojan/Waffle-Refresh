using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
 

namespace WaffleRefresh
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                if (Array.Exists(args, a => string.Equals(a, "--oneshot", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "--apply", StringComparison.OrdinalIgnoreCase)))
                {
                    var isAc = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;
                    int target = isAc ? 165 : 60;
                    DisplayHelper.TrySetPrimaryDisplayRefreshRate(target, out _);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => Log.Write("ThreadException: " + e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write("UnhandledException: " + e.ExceptionObject);
                System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Idle;
                Log.Write("App start");
                Application.Run(new TrayApp());
                Log.Write("App exit");
            }
            catch (Exception ex)
            {
                Log.Write("Main Exception: " + ex);
                throw;
            }
        }
    }

    internal static class Log
    {
        private static readonly string LogDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Waffle-Refresh");
        private static readonly string PathStr = System.IO.Path.Combine(LogDir, "prs.log");
        [Conditional("DEBUG")]
        public static void Write(string msg)
        {
            try
            {
                System.IO.Directory.CreateDirectory(LogDir);
                var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + msg + Environment.NewLine;
                System.IO.File.AppendAllText(PathStr, line);
            }
            catch { }
        }
    }

    internal class TrayApp : Form
    {
        private readonly int AcHz = 165;
        private readonly int DcHz = 60;
        private NotifyIcon _tray = null!;
        private bool _enabled = true;
        private int _lastAppliedHz = 0;
        private IntPtr _powerRegHandle = IntPtr.Zero;

        protected override void OnLoad(EventArgs e)
        {
            Visible = false;
            Log.Write("OnLoad");
            ShowInTaskbar = false;

            var appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            _tray = new NotifyIcon
            {
                Icon = appIcon,
                Visible = true,
                Text = "Waffle-Refresh"
            };
            var ctx = new ContextMenuStrip();
            var enabledItem = new ToolStripMenuItem("자동 전환 사용", null, (s, a) => { _enabled = !_enabled; ((ToolStripMenuItem)s!).Checked = _enabled; })
            { Checked = true, CheckOnClick = true };
            ctx.Items.Add(enabledItem);
            ctx.Items.Add(new ToolStripMenuItem("지금 적용", null, (s, a) => ApplyNow()));
            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add(new ToolStripMenuItem("종료", null, (s, a) => Close()));
            _tray.ContextMenuStrip = ctx;

            

            Log.Write("Before DetectAndSwitch");
            DetectAndSwitch();
            Log.Write("OnLoad complete");
            base.OnLoad(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode)
            {
                Log.Write("Registering power notifications");
                _powerRegHandle = RegisterForPowerNotifications(this.Handle);
                Log.Write("Registered power notifications");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_powerRegHandle != IntPtr.Zero)
            {
                Log.Write("Unregister power notifications");
                UnregisterPowerSettingNotification(_powerRegHandle);
            }
            _tray?.Dispose();
            Log.Write("Form closing");
            base.OnFormClosing(e);
        }

        private void ApplyNow()
        {
            DetectAndSwitch(true);
        }

        private void DetectAndSwitch(bool force = false)
        {
            Log.Write($"DetectAndSwitch(force={force}) enabled={_enabled}");
            if (!_enabled) return;

            var isAc = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;
            int target = isAc ? AcHz : DcHz;
            Log.Write($"Power status isAc={isAc}, target={target}");
            if (force || _lastAppliedHz != target)
            {
                if (DisplayHelper.TrySetPrimaryDisplayRefreshRate(target, out int applied))
                {
                    _lastAppliedHz = applied;
                    if (_tray != null) _tray.Text = $"Waffle-Refresh ({(isAc ? "전원" : "배터리")} · {applied}Hz)";
                    Log.Write($"Applied refresh rate: {applied}Hz");
                }
                else
                {
                    if (_tray != null) _tray.Text = $"Waffle-Refresh (변경 실패)";
                    Log.Write("Failed to apply refresh rate");
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_POWERBROADCAST = 0x0218;
            const int WM_DISPLAYCHANGE = 0x007E;
            const int PBT_POWERSETTINGCHANGE = 0x8013;

            if (m.Msg == WM_POWERBROADCAST && m.WParam.ToInt32() == PBT_POWERSETTINGCHANGE)
            {
                var setting = Marshal.PtrToStructure<POWERBROADCAST_SETTING>(m.LParam);
                if (setting.PowerSetting == GUID_ACDC_POWER_SOURCE)
                {
                    Log.Write("WM_POWERBROADCAST: power setting change");
                    DetectAndSwitch();
                }
            }
            else if (m.Msg == WM_DISPLAYCHANGE)
            {
                DisplayHelper.ClearCache();
            }
            base.WndProc(ref m);
        }

        private static Guid GUID_ACDC_POWER_SOURCE = new Guid("5D3E9A59-E9D5-4B00-A6BD-FF34FF516548");

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct POWERBROADCAST_SETTING
        {
            public Guid PowerSetting;
            public uint DataLength;
            public byte Data;
        }

        private static IntPtr RegisterForPowerNotifications(IntPtr hwnd)
        {
            const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;
            return RegisterPowerSettingNotification(hwnd, ref GUID_ACDC_POWER_SOURCE, DEVICE_NOTIFY_WINDOW_HANDLE);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid PowerSettingGuid, int Flags);

        [DllImport("user32.dll")]
        private static extern bool UnregisterPowerSettingNotification(IntPtr Handle);
    }

    internal static class DisplayHelper
    {
        private static bool s_cacheValid;
        private static int s_w, s_h, s_bpp;
        private static DEVMODE? s_best60;
        private static DEVMODE? s_best165;
        private static readonly List<DEVMODE> s_modesBuffer = new List<DEVMODE>(64);

        public static void ClearCache()
        {
            s_cacheValid = false;
            s_best60 = null;
            s_best165 = null;
        }

        public static bool TrySetPrimaryDisplayRefreshRate(int desiredHz, out int appliedHz)
        {
            appliedHz = 0;
            Log.Write($"TrySetPrimaryDisplayRefreshRate desired={desiredHz}");
            if (!EnumCurrent(out var current)) { Log.Write("EnumCurrent failed"); return false; }

            if (!s_cacheValid || current.dmPelsWidth != s_w || current.dmPelsHeight != s_h || current.dmBitsPerPel != s_bpp)
            {
                var modesAll = EnumerateModesMatching(current.dmPelsWidth, current.dmPelsHeight, current.dmBitsPerPel);
                s_w = current.dmPelsWidth; s_h = current.dmPelsHeight; s_bpp = current.dmBitsPerPel;
                s_best60 = FindClosest(modesAll, 60);
                s_best165 = FindClosest(modesAll, 165);
                s_cacheValid = true;
            }

            DEVMODE? best = desiredHz == 60 ? s_best60 : (desiredHz == 165 ? s_best165 : FindClosest(EnumerateModesMatching(current.dmPelsWidth, current.dmPelsHeight, current.dmBitsPerPel), desiredHz));
            if (best == null) return false;

            var target = best.Value;
            if (target.dmDisplayFrequency == current.dmDisplayFrequency)
            {
                appliedHz = current.dmDisplayFrequency;
                return true;
            }
            int result = ChangeDisplaySettingsEx(null, ref target, IntPtr.Zero, 0, IntPtr.Zero);
            if (result == DISP_CHANGE_SUCCESSFUL)
            {
                appliedHz = target.dmDisplayFrequency;
                Log.Write($"ChangeDisplaySettingsEx applied={appliedHz}Hz");
                return true;
            }

            var t2 = current;
            t2.dmDisplayFrequency = desiredHz;
            t2.dmFields |= DM_DISPLAYFREQUENCY;
            result = ChangeDisplaySettingsEx(null, ref t2, IntPtr.Zero, 0, IntPtr.Zero);
            if (result == DISP_CHANGE_SUCCESSFUL)
            {
                appliedHz = desiredHz;
                Log.Write($"ChangeDisplaySettingsEx forced applied={appliedHz}Hz");
                return true;
            }
            Log.Write($"ChangeDisplaySettingsEx result={result}");
            return false;
        }

        private static DEVMODE? FindClosest(List<DEVMODE> modes, int desired)
        {
            if (modes.Count == 0) return null;
            DEVMODE? best = null;
            int bestDiff = int.MaxValue;
            foreach (var m in modes)
            {
                if (m.dmDisplayFrequency <= 1) continue;
                int diff = Math.Abs(m.dmDisplayFrequency - desired);
                if (diff < bestDiff)
                {
                    best = m;
                    bestDiff = diff;
                    if (diff == 0) break;
                }
            }
            return best;
        }

        private static bool EnumCurrent(out DEVMODE dev)
        {
            dev = CreateDevMode();
            return EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dev);
        }

        private static List<DEVMODE> EnumerateModesMatching(int width, int height, int bpp)
        {
            s_modesBuffer.Clear();
            int i = 0;
            while (true)
            {
                var dm = CreateDevMode();
                if (!EnumDisplaySettings(null, i, ref dm)) break;
                if (dm.dmPelsWidth == width && dm.dmPelsHeight == height && dm.dmBitsPerPel == bpp)
                {
                    s_modesBuffer.Add(dm);
                }
                i++;
            }
            return s_modesBuffer;
        }

        private static DEVMODE CreateDevMode()
        {
            var dm = new DEVMODE();
            dm.dmDeviceName = new string('\0', 32);
            dm.dmFormName = new string('\0', 32);
            dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            return dm;
        }

        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int DISP_CHANGE_SUCCESSFUL = 0;
        private const int DM_DISPLAYFREQUENCY = 0x00400000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int ChangeDisplaySettingsEx(string? lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);
    }
}
