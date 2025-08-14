using System.Windows;
using System.Threading.Tasks;
using DemoLegal.Infrastructure.Repositories;

namespace DemoLegal.Wpf;

public partial class CandidatesWindow : Window
{
    private readonly ICaseQueryRepository _repo = new CaseQueryRepository();

    public CandidatesWindow()
    {
        InitializeComponent();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var data = await _repo.GetCandidatesAsync(500);
        GridCases.ItemsSource = data;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }
}
