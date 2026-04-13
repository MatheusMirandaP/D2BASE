using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace CirclePointDistributorInstaller;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        InstallPathTxt.Text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D2BASE");
        
        // Check for uninstall argument
        var args = Environment.GetCommandLineArgs();
        if (args.Contains("/uninstall"))
        {
            this.Loaded += async (s, e) => {
                await Task.Run(() => PerformUninstall());
                Application.Current.Shutdown();
            };
        }
    }

    private void BrowseBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() == true)
        {
            InstallPathTxt.Text = System.IO.Path.Combine(dialog.FolderName, "D2BASE");
        }
    }

    private async void InstallBtn_Click(object sender, RoutedEventArgs e)
    {
        string targetDir = InstallPathTxt.Text;
        if (string.IsNullOrWhiteSpace(targetDir)) return;

        InstallBtn.IsEnabled = false;
        InstallPathTxt.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;

        await Task.Run(() => PerformInstall(targetDir));
    }

    private void PerformInstall(string targetDir)
    {
        try
        {
            Dispatcher.Invoke(() => StatusTxt.Text = "Criando diretórios...");
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            Dispatcher.Invoke(() => StatusTxt.Text = "Extraindo arquivos...");
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = "CirclePointDistributorInstaller.Resources.D2BASE.exe";

            string destPath = System.IO.Path.Combine(targetDir, "D2BASE.exe");
            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) 
                {
                    string[] names = assembly.GetManifestResourceNames();
                    throw new Exception($"Embedded resource '{resourceName}' not found! Available: {string.Join(", ", names)}");
                }
                
                using (FileStream fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fileStream);
                }
            }

            // Copy installer as uninstaller
            string uninstallerPath = System.IO.Path.Combine(targetDir, "uninstall.exe");
            File.Copy(Process.GetCurrentProcess().MainModule?.FileName ?? "", uninstallerPath, true);

            Dispatcher.Invoke(() => StatusTxt.Text = "Criando atalho...");
            CreateShortcut(targetDir);

            Dispatcher.Invoke(() => StatusTxt.Text = "Registrando no sistema...");
            RegisterFileAssociation(destPath);
            RegisterUninstaller(targetDir, uninstallerPath);

            Dispatcher.Invoke(() => 
            {
                StatusTxt.Text = "Concluído!";
                MessageBox.Show("Instalação concluída com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Shutdown();
            });
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => 
            {
                StatusTxt.Text = "Erro!";
                MessageBox.Show($"A instalação falhou: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                InstallBtn.IsEnabled = true;
                InstallPathTxt.IsEnabled = true;
                ProgressPanel.Visibility = Visibility.Collapsed;
            });
        }
    }

    private void RegisterFileAssociation(string exePath)
    {
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.res"))
            {
                key.SetValue("", "D2BASE.Project");
            }

            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\D2BASE.Project"))
            {
                key.SetValue("", "Projeto D2BASE");
                using (var shellKey = key.CreateSubKey(@"shell\open\command"))
                {
                    shellKey.SetValue("", $"\"{exePath}\" \"%1\"");
                }
            }
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => MessageBox.Show($"Aviso: Não foi possível registrar a associação de arquivo. {ex.Message}"));
        }
    }

    private void RegisterUninstaller(string targetDir, string uninstallerPath)
    {
        try
        {
            string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\D2BASE";
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                key.SetValue("DisplayName", "D2BASE");
                key.SetValue("UninstallString", $"\"{uninstallerPath}\" /uninstall");
                key.SetValue("DisplayIcon", System.IO.Path.Combine(targetDir, "D2BASE.exe"));
                key.SetValue("Publisher", "Matheus");
                key.SetValue("InstallLocation", targetDir);
                key.SetValue("DisplayVersion", "1.0.0");
            }
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => MessageBox.Show($"Aviso: Não foi possível registrar o desinstalador. {ex.Message}"));
        }
    }

    private void PerformUninstall()
    {
        try
        {
            // Remove Registry Keys
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.res", false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\D2BASE.Project", false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\D2BASE", false);

            // Remove Shortcut
            string shortcutPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "D2BASE.lnk");
            if (File.Exists(shortcutPath)) File.Delete(shortcutPath);

            MessageBox.Show("O programa foi removido, mas alguns arquivos no diretório de instalação podem precisar de exclusão manual.", "Desinstalação", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro durante a desinstalação: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateShortcut(string targetDir)
    {
        try 
        {
            string exePath = System.IO.Path.Combine(targetDir, "D2BASE.exe");
            string shortcutPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "D2BASE.lnk");
            
            // PowerShell command to create shortcut - escape single quotes for literal strings
            string escapedShortcutPath = shortcutPath.Replace("'", "''");
            string escapedExePath = exePath.Replace("'", "''");
            string script = $"$s=(New-Object -ComObject WScript.Shell).CreateShortcut('{escapedShortcutPath}'); $s.TargetPath='{escapedExePath}'; $s.IconLocation='{escapedExePath},0'; $s.Save()";

            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = System.Diagnostics.Process.Start(processStartInfo))
            {
                if (process != null) process.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            // Non-critical error, but good to log or show if debugging
            Dispatcher.Invoke(() => MessageBox.Show($"Aviso: Não foi possível criar o atalho. {ex.Message}"));
        }
    }
}
