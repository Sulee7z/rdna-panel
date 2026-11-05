using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;


internal static class Program
{
    private static string Target => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "RadeonTune");

    [STAThread]
    private static int Main(string[] args)
    {
        var logPath = Path.Combine(Path.GetTempPath(), "RadeonTuneSetup.log");
        File.WriteAllText(logPath, "start " + DateTime.Now + Environment.NewLine);
        bool silent = Array.Exists(args, a => a.Equals("/S", StringComparison.OrdinalIgnoreCase) || a.Equals("/silent", StringComparison.OrdinalIgnoreCase));
        try
        {
            var answer = silent ? DialogResult.OK : MessageBox.Show(
                "Install RadeonTune for the current user?\n\n" +
                "目标目录：" + Target + "\n\n安装后会在开始菜单和桌面创建快捷方式。",
                "RadeonTune Setup", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (answer != DialogResult.OK) return 0;

            if (Directory.Exists(Target)) Directory.Delete(Target, true);
            Directory.CreateDirectory(Target);

            File.AppendAllText(logPath, "extract to " + Target + Environment.NewLine);
            var asm = Assembly.GetExecutingAssembly();
            using (var s = asm.GetManifestResourceStream("payload.zip"))
            {
                File.AppendAllText(logPath, "payload stream: " + (s == null ? "null" : "ok") + Environment.NewLine);
            }
            using (var s = asm.GetManifestResourceStream("payload.zip"))
            using (var z = new ZipArchive(s, ZipArchiveMode.Read))
                z.ExtractToDirectory(Target, true);

            var exe = Path.Combine(Target, "RadeonTune.exe");
            CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "RadeonTune.lnk"), exe, Target);
            CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RadeonTune.lnk"), exe, Target);

            var uninst = Path.Combine(Target, "uninstall.ps1");
            if (File.Exists(uninst))
            {
                var psCmd = "$k='HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\RadeonTune'; " +
                            "New-Item $k -Force | Out-Null; " +
                            "Set-ItemProperty $k DisplayName RadeonTune; " +
                            "Set-ItemProperty $k DisplayVersion 1.2.6; " +
                            "Set-ItemProperty $k Publisher RadeonTune; " +
                            "Set-ItemProperty $k InstallLocation '" + Target + "'; " +
                            "Set-ItemProperty $k DisplayIcon '" + exe + "'; " +
                            "Set-ItemProperty $k UninstallString ('powershell -NoProfile -ExecutionPolicy Bypass -File \"" + uninst + "\"')";
                Process.Start(new ProcessStartInfo("powershell", "-NoProfile -ExecutionPolicy Bypass -Command \"" + psCmd + "\"")
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }).WaitForExit();
            }

            if (!silent) MessageBox.Show("RadeonTune installed.\n\n" + Target, "RadeonTune Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (!silent) Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Target, UseShellExecute = true });
            return 0;
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "RadeonTuneSetup.log"), "ERROR " + ex + Environment.NewLine); } catch { }
            if (!silent) MessageBox.Show("Install failed:\n\n" + ex.Message, "RadeonTune Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static void CreateShortcut(string path, string exe, string workdir)
    {
        if (File.Exists(path)) File.Delete(path);
        var t = Type.GetTypeFromProgID("WScript.Shell");
        dynamic shell = Activator.CreateInstance(t);
        dynamic lnk = shell.CreateShortcut(path);
        lnk.TargetPath = exe;
        lnk.WorkingDirectory = workdir;
        lnk.IconLocation = exe;
        lnk.Save();
    }
}
