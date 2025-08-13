using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DemoLegal.Application.Abstractions;
using DemoLegal.Application.DTOs;
using DemoLegal.Infrastructure.Files;
using DemoLegal.Wpf.Commands;
using DemoLegal.Wpf.Utils;

namespace DemoLegal.Wpf.ViewModels;

public sealed class CasesViewModel : INotifyPropertyChanged
{
    private readonly ICaseQueries _queries;
    private readonly IDocumentService _docService;

    public CasesViewModel(ICaseQueries queries, IDocumentService docService)
    {
        _queries = queries;
        _docService = docService;

        RefreshCommand = new AsyncCommand(RefreshAsync);
        BuildPretrialCommand = new AsyncCommand<Guid?>(BuildPretrialAsync, caseId => caseId.HasValue && caseId != Guid.Empty);
        OpenFolderCommand = new AsyncCommand<Guid?>(OpenFolderAsync, caseId => caseId.HasValue && caseId != Guid.Empty);
    }

    public ObservableCollection<CaseFileDto> Items { get; } = new();
    private CaseFileDto? _selected;
    public CaseFileDto? Selected
    {
        get => _selected;
        set
        {
            _selected = value; OnPropertyChanged();
            (BuildPretrialCommand as AsyncCommand<Guid?>)?.RaiseCanExecuteChanged();
            (OpenFolderCommand as AsyncCommand<Guid?>)?.RaiseCanExecuteChanged();
        }
    }

    public ObservableCollection<string> Messages { get; } = new();

    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand<Guid?> BuildPretrialCommand { get; }
    public AsyncCommand<Guid?> OpenFolderCommand { get; }

    private async Task RefreshAsync()
    {
        Items.Clear();
        var list = await _queries.GetRecentCasesAsync(200).ConfigureAwait(false);
        foreach (var c in list) Items.Add(c);
        Messages.Add($"Загружено дел: {Items.Count}");
    }

    private async Task BuildPretrialAsync(Guid? caseId)
    {
        if (!caseId.HasValue) return;
        var result = await _docService.BuildPretrialAsync(caseId.Value).ConfigureAwait(false);
        if (result.Success)
            Messages.Add($"Досудебный пакет собран. Папка: {result.OutputFolder}");
        else
            Messages.Add($"Не удалось собрать пакет: {string.Join("; ", result.Warnings)}");
    }

    private Task OpenFolderAsync(Guid? caseId)
    {
        if (!caseId.HasValue) return Task.CompletedTask;
        var folder = PathService.GetCaseFolder(caseId.Value);
        FileExplorer.OpenFolder(folder);
        Messages.Add($"Открыта папка дела: {folder}");
        return Task.CompletedTask;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
