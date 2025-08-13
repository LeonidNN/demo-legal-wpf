using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DemoLegal.Application.Abstractions;
using DemoLegal.Application.DTOs;
using DemoLegal.Wpf.Commands;

namespace DemoLegal.Wpf.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IImporter _importer;
    private readonly IAfterImportCaseBuilder _caseBuilder;

    public MainViewModel(IImporter importer, IAfterImportCaseBuilder caseBuilder)
    {
        _importer = importer;
        _caseBuilder = caseBuilder;
        ImportCommand = new AsyncCommand(ImportAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(SelectedPath));
    }

    private string? _selectedPath;
    public string? SelectedPath
    {
        get => _selectedPath;
        set { _selectedPath = value; OnPropertyChanged(); (ImportCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); (ImportCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }

    private int _rowsRead, _rowsImported, _errors, _casesBuilt;
    public int RowsRead { get => _rowsRead; private set { _rowsRead = value; OnPropertyChanged(); } }
    public int RowsImported { get => _rowsImported; private set { _rowsImported = value; OnPropertyChanged(); } }
    public int Errors { get => _errors; private set { _errors = value; OnPropertyChanged(); } }
    public int CasesBuilt { get => _casesBuilt; private set { _casesBuilt = value; OnPropertyChanged(); } }

    public ObservableCollection<string> Messages { get; } = new();

    public AsyncCommand ImportCommand { get; }

    private async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedPath)) return;
        try
        {
            IsBusy = true;
            Messages.Clear();
            RowsRead = RowsImported = Errors = CasesBuilt = 0;

            ImportReportDto report = await _importer.ImportAsync(SelectedPath!).ConfigureAwait(false);
            RowsRead = report.RowsRead;
            RowsImported = report.RowsImported;
            Errors = report.Errors;
            foreach (var m in report.Messages)
                Messages.Add(m);

            // после импорта — построить/обновить дела по всем ЛС
            CasesBuilt = await _caseBuilder.BuildCasesForAllAccountsAsync().ConfigureAwait(false);
            Messages.Add($"Сформировано/обновлено дел: {CasesBuilt}");
        }
        catch (Exception ex)
        {
            Messages.Add($"Ошибка: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
