using System;
using System.Threading.Tasks;
using System.Windows;
using DemoLegal.Infrastructure.Extensions;
using DemoLegal.Infrastructure.Persistence;
using DemoLegal.Wpf.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DemoLegal.Wpf;

public partial class App : Application
{
    private IHost? _host;

    public IServiceProvider Services => _host!.Services;

    public static App CurrentApp => (App)Application.Current;

    public static T GetService<T>() where T : notnull
        => (T)CurrentApp.Services.GetService(typeof(T))!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(cfg =>
            {
                cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((ctx, services) =>
            {
                var dbPath = ctx.Configuration["Storage:DatabasePath"];
                services.AddDemoLegalInfrastructure(dbPath);

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<CasesViewModel>();
            })
            .Build();

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DemoContext>();
            await DbInitializer.EnsureCreatedAsync(db).ConfigureAwait(false);
        }

        await _host.StartAsync().ConfigureAwait(false);

        var vm = _host.Services.GetRequiredService<MainViewModel>();
        var window = new Views.MainWindow { DataContext = vm };
        window.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
            await _host.StopAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        base.OnExit(e);
    }
}
