using System.IO;
using System.Windows;
using PtzStudio.Core.Storage;

namespace PtzStudio.App;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            try
            {
                var dir = Path.Combine(SettingsStore.DefaultDirectory(), "logs");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "erros.log"), $"{DateTime.Now:s} {e.Exception}\n\n");
            }
            catch { }
            MessageBox.Show("Ocorreu um erro inesperado, mas o PTZ Studio continua aberto.\n\n" + e.Exception.Message,
                "PTZ Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
        };
    }
}
