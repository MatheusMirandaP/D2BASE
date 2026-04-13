using System.Windows;
using System.Windows.Markup;
using System.Globalization;
using System.Threading;
using System.IO;

namespace CirclePointDistributor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var culture = new CultureInfo("pt-BR");
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        // Force WPF to use current culture for formatting in bindings
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.Name)));

        string? initialFile = null;
        if (e.Args.Length > 0)
        {
            // If there's only one arg, it's likely the path (possibly quoted)
            if (e.Args.Length == 1)
            {
                initialFile = e.Args[0];
            }
            else
            {
                // If multiple args, try joining them in case it's an unquoted path with spaces
                // but first check if the first arg exists as a file.
                if (File.Exists(e.Args[0]))
                {
                    initialFile = e.Args[0];
                }
                else
                {
                    initialFile = string.Join(" ", e.Args);
                }
            }
        }

        MainWindow mainWindow = new MainWindow(initialFile);
        mainWindow.Show();

        base.OnStartup(e);
    }
}

