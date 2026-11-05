using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace RadeonTune
{
    /// <summary>托盘图标：显示主界面 / 应用开机预设 / 退出。</summary>
    internal static class TrayHost
    {
        private static NotifyIcon _icon;
        private static Window _window;

        private static void Log(string msg)
        {
            try
            {
                var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RDNA Panel");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "tray.log"),
                    $"{DateTime.Now:HH:mm:ss} {msg}{Environment.NewLine}");
            }
            catch { }
        }

        /// <summary>生成高对比度托盘图标（深色任务栏上必须能看清）。</summary>
        private static System.Drawing.Bitmap _trayBmp;   // 必须活着，否则 HICON 失效 → 托盘空白
        private static IntPtr _trayHicon;
        private static Icon _trayIconCache;

        private static Icon MakeTrayIcon()
        {
            try
            {
                if (_trayIconCache != null) return _trayIconCache;
                int sz = 16;
                var bmp = new System.Drawing.Bitmap(sz, sz);
                var g = System.Drawing.Graphics.FromImage(bmp);
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    var red = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0xE8, 0x20, 0x37));
                    g.FillEllipse(red, 0, 0, sz - 1, sz - 1);
                    var font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
                    var fmt = new System.Drawing.StringFormat
                    {
                        Alignment = System.Drawing.StringAlignment.Center,
                        LineAlignment = System.Drawing.StringAlignment.Center,
                    };
                    g.DrawString("R", font, System.Drawing.Brushes.White, new System.Drawing.RectangleF(0, 0, sz, sz), fmt);
                }
                _trayBmp = bmp;
                g.Dispose();
                _trayHicon = bmp.GetHicon();
                _trayIconCache = Icon.FromHandle(_trayHicon);
                return _trayIconCache;
            }
            catch (Exception ex) { Log("tray icon build failed: " + ex.Message); return null; }
        }

        public static void Attach(Window window)
        {
            Log("attach called");
            if (_icon != null) { Log("already attached"); return; }
            _window = window;
            Icon ico = MakeTrayIcon();
            if (ico == null)
            try
            {
                var res = System.Windows.Application.GetResourceStream(new Uri("app.ico", UriKind.Relative));
                if (res != null) ico = new Icon(res.Stream, 16, 16);
            }
            catch (Exception ex) { Log("ico resource failed: " + ex.Message); }
            if (ico == null)
            {
                try { ico = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location); }
                catch (Exception ex) { Log("icon extract failed: " + ex.Message); }
            }
            if (ico == null) ico = SystemIcons.Application;

            var menu = new ContextMenuStrip();
            menu.Items.Add("显示主界面", null, (_, __) => ShowWindow());
            menu.Items.Add("立即应用开机预设", null, (_, __) =>
            {
                try
                {
                    var auto = Engine.LoadProfiles().Values.FirstOrDefault(x => x.AutoApply && x.HasOc);
                    if (auto != null) Engine.ApplySafe(auto);
                }
                catch { }
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (_, __) => ExitApp());

            _icon = new NotifyIcon
            {
                Icon = ico,
                Text = "RDNA Panel",
                Visible = true,
                ContextMenuStrip = menu,
            };
            _icon.DoubleClick += (_, __) => ShowWindow();
            Log($"attached visible={_icon.Visible} iconNull={ico == null}");
        }

        public static void ShowWindow()
        {
            if (_window == null) return;
            _window.ShowInTaskbar = true;
            _window.Show();
            if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
            _window.Activate();
        }

        public static void HideToTray()
        {
            if (_window == null) return;   // 需要先 Attach（MainWindow 构造时已调用）
            _window.ShowInTaskbar = false;
            _window.Hide();
            if (_icon == null) Attach(_window);

            Log($"hidden to tray, iconVisible={_icon?.Visible}");
            try
            {
                _icon?.ShowBalloonTip(3000, "RDNA Panel", "已最小化到托盘，双击图标恢复", ToolTipIcon.Info);
            }
            catch { }
        }

        public static void ExitApp()
        {
            if (_icon != null) { _icon.Visible = false; _icon.Dispose(); _icon = null; }
            System.Windows.Application.Current.Shutdown();
        }
    }
}
