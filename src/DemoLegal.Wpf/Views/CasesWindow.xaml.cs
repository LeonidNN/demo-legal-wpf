using System.Windows;

namespace DemoLegal.Wpf.Views;

public partial class CasesWindow : Window
{
    public CasesWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is DemoLegal.Wpf.ViewModels.CasesViewModel vm)
        {
            vm.RefreshCommand.Execute(null);
        }
    }
}
