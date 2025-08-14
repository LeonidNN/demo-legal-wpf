using System.Windows;
using DemoLegal.Infrastructure.Database;

namespace DemoLegal.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var dbPath = DbBootstrap.EnsureDatabase();
            var wnd = new MainWindow();
            wnd.Title += $"  БД: {dbPath}";
            wnd.Show();
        }
        catch (System.Exception ex)
        {
            MessageBox.Show("Ошибка инициализации БД: " + ex.Message, "DemoLegal",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }
}
