using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;

namespace DemoLegal.Wpf.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var conv = new BooleanToBusyConverter();
        Resources["BooleanToBusyConverter"] = conv;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Все поддерживаемые (*.csv;*.xlsx)|*.csv;*.xlsx|CSV файлы (*.csv)|*.csv|Excel файлы (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dlg.ShowDialog(this) == true)
        {
            dynamic? vm = this.DataContext;
            try { vm.SelectedPath = dlg.FileName; } catch { /* ignore */ }
        }
    }

    private void OpenCases_Click(object sender, RoutedEventArgs e)
    {
        var vm = DemoLegal.Wpf.App.GetService<DemoLegal.Wpf.ViewModels.CasesViewModel>();
        var wnd = new CasesWindow { Owner = this, DataContext = vm };
        wnd.ShowDialog();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var wnd = new AboutWindow { Owner = this };
        wnd.ShowDialog();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenCasesRoot_Click(object sender, RoutedEventArgs e)
    {
        var root = DemoLegal.Infrastructure.Files.PathService.GetCasesRoot();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = root,
            UseShellExecute = true
        });
    }
}

/// <summary>true  "Выполняется...", false  "".</summary>
public sealed class BooleanToBusyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? "Выполняется..." : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing!;
}
