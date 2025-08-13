using System.Threading.Tasks;
using DemoLegal.Application.DTOs;
using DemoLegal.Application.Abstractions;

namespace DemoLegal.Application.UseCases.Import;

/// <summary>Команда: импорт файла (xlsx/csv) и формирование/обновление дел.</summary>
public sealed class ImportFileCommand
{
    public string Path { get; }

    public ImportFileCommand(string path) => Path = path;
}

/// <summary>Хэндлер команды импорта: вызывает IImporter и возвращает отчёт.</summary>
public sealed class ImportFileCommandHandler
{
    private readonly IImporter _importer;

    public ImportFileCommandHandler(IImporter importer)
    {
        _importer = importer;
    }

    public Task<ImportReportDto> HandleAsync(ImportFileCommand cmd)
        => _importer.ImportAsync(cmd.Path);
}
