using System;
using System.Windows;
using System.Windows.Threading;
using KEYCE_KODER.App.Views;

namespace KEYCE_KODER.App;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        try
        {
            var application = new App();
            var splash = new SplashWindow();

            application.MainWindow = splash;
            splash.Show();

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };

            timer.Tick += (_, _) =>
            {
                timer.Stop();

                var mainWindow = new MainWindow();

                application.MainWindow = mainWindow;
                mainWindow.Show();

                splash.Close();
            };

            timer.Start();
            application.Run();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Erreur de démarrage — KEYCE KODER",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}