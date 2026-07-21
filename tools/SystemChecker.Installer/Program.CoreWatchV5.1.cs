using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Forms = System.Windows.Forms;

namespace CoreWatch.Installer;

internal static class Program
{
    private const string Product = "CoreWatch";
    private const string Version = "5.10.0";
    private const string PawnIoUrl = "https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe";
    private const string PawnIoSha256 = "1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032";
    private sealed record PawnIoResult(string Message, bool RebootRequired, bool Failed);

    [STAThread]
    private static void Main(string[] args)
    {
        try { if (args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase)) Uninstall(); else Install(); }
        catch (Exception ex) { Forms.MessageBox.Show(ex.Message, "CoreWatch 설치 오류", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error); }
    }

    private static void Install()
    {
        if (Forms.MessageBox.Show("CoreWatch와 PawnIO 센서 드라이버를 설치하시겠습니까?\n\nPawnIO 2.2.0은 공식 GitHub에서 다운로드하고 SHA-256을 검증한 뒤 자동 설치합니다.", "CoreWatch Setup", Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Question) != Forms.DialogResult.Yes) return;
        PawnIoResult pawnIo;
        try { pawnIo = InstallPawnIoAsync().GetAwaiter().GetResult(); }
        catch (Exception ex) { pawnIo = new PawnIoResult($"PawnIO 구성은 완료하지 못했습니다: {ex.Message}\nCoreWatch는 정상 설치되며 센서 이외 기능을 사용할 수 있습니다.", false, true); }
        var directory = InstallDirectory();
        Directory.CreateDirectory(Path.Combine(directory, "tools"));
        Directory.CreateDirectory(Path.Combine(directory, "Assets"));
        Extract("Payload.CoreWatch.exe", Path.Combine(directory, "CoreWatch.exe"));
        Extract("Payload.diskspd.exe", Path.Combine(directory, "tools", "diskspd.exe"));
        Extract("Payload.DiskSpd-EULA.txt", Path.Combine(directory, "tools", "DiskSpd-EULA.txt"));
        Extract("Payload.CoreWatch.ico", Path.Combine(directory, "Assets", "CoreWatch.ico"));
        var self = Environment.ProcessPath ?? throw new InvalidOperationException("설치 프로그램 경로를 찾을 수 없습니다.");
        File.Copy(self, Path.Combine(directory, "CoreWatch-Uninstall.exe"), true);
        CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "CoreWatch.lnk"), Path.Combine(directory, "CoreWatch.exe"));
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\CoreWatch");
        key.SetValue("DisplayName", Product); key.SetValue("DisplayVersion", Version); key.SetValue("Publisher", "CoreWatch Project"); key.SetValue("InstallLocation", directory); key.SetValue("DisplayIcon", Path.Combine(directory, "CoreWatch.exe")); key.SetValue("UninstallString", $"\"{Path.Combine(directory, "CoreWatch-Uninstall.exe")}\" --uninstall"); key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1);
        var message = $"CoreWatch 설치가 완료되었습니다.\n\n{pawnIo.Message}";
        Forms.MessageBox.Show(message, "CoreWatch Setup", Forms.MessageBoxButtons.OK, pawnIo.Failed ? Forms.MessageBoxIcon.Warning : Forms.MessageBoxIcon.Information);
        if (!pawnIo.RebootRequired) Process.Start(new ProcessStartInfo(Path.Combine(directory, "CoreWatch.exe")) { UseShellExecute = true });
    }

    private static async Task<PawnIoResult> InstallPawnIoAsync()
    {
        var installed = PawnIoInstallDetector.GetInstalledVersion();
        if (installed is not null && !PawnIoInstallDetector.RequiresInstall(installed))
            return new PawnIoResult($"PawnIO {installed}이(가) 이미 설치되어 있어 드라이버 설치를 건너뛰었습니다.", false, false);

        var temporary = Path.Combine(Path.GetTempPath(), "CoreWatch-PawnIO-2.2.0.exe");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            await using (var output = File.Create(temporary)) await (await client.GetStreamAsync(PawnIoUrl)).CopyToAsync(output);
            await using var input = File.OpenRead(temporary);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(input));
            if (!hash.Equals(PawnIoSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("PawnIO 설치 파일의 무결성 검증에 실패했습니다.");
            using var process = Process.Start(new ProcessStartInfo(temporary, "-install -silent") { UseShellExecute = false, CreateNoWindow = true }) ?? throw new InvalidOperationException("PawnIO 설치 프로그램을 시작하지 못했습니다.");
            process.WaitForExit();
            if (process.ExitCode is not 0 and not 3010)
            {
                installed = PawnIoInstallDetector.GetInstalledVersion();
                if (installed is not null && !PawnIoInstallDetector.RequiresInstall(installed))
                    return new PawnIoResult($"PawnIO {installed} 설치 상태를 확인했습니다.", false, false);
                throw new InvalidOperationException($"PawnIO 설치 프로그램 종료 코드 {process.ExitCode}");
            }
            return process.ExitCode == 3010
                ? new PawnIoResult("PawnIO 설치가 완료되었습니다. 드라이버 적용을 위해 Windows를 다시 시작하세요.", true, false)
                : new PawnIoResult("PawnIO 2.2.0 설치가 완료되었습니다.", false, false);
        }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
    }

    private static void Uninstall()
    {
        if (Forms.MessageBox.Show("CoreWatch 프로그램 파일을 제거하시겠습니까?\n측정 기록과 다른 프로그램이 사용할 수 있는 PawnIO 드라이버는 보존됩니다.", "CoreWatch 제거", Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Warning) != Forms.DialogResult.Yes) return;
        var directory = Path.GetFullPath(InstallDirectory());
        var allowedRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")) + Path.DirectorySeparatorChar;
        if (!directory.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("안전하지 않은 제거 경로입니다.");
        var shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "CoreWatch.lnk");
        if (File.Exists(shortcut)) File.Delete(shortcut);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\CoreWatch", false);
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{directory}\"") { UseShellExecute = false, CreateNoWindow = true });
    }

    private static string InstallDirectory() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "CoreWatch");
    private static void Extract(string resource, string path) { using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource) ?? throw new InvalidOperationException($"설치 리소스 누락: {resource}"); using var target = File.Create(path); source.CopyTo(target); }
    private static void CreateShortcut(string path, string target) { var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("바로가기 서비스를 사용할 수 없습니다."); dynamic shell = Activator.CreateInstance(type)!; dynamic shortcut = shell.CreateShortcut(path); shortcut.TargetPath = target; shortcut.WorkingDirectory = Path.GetDirectoryName(target); shortcut.Description = Product; shortcut.Save(); }
}




