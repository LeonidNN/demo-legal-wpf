using System.IO;
using System.Threading.Tasks;
using DemoLegal.Application.Abstractions;
using DemoLegal.Application.DTOs;

namespace DemoLegal.Infrastructure.Import;

/// <summary>
/// Делегирует импорт подходящему импортеру по расширению файла.
/// </summary>
public sealed class CompositeImporter : IImporter
{
    private readonly CsvImporter _csv;
    private readonly XlsxImporter _xlsx;

    public CompositeImporter(CsvImporter csv, XlsxImporter xlsx)
    {
        _csv = csv;
        _xlsx = xlsx;
    }

    public Task<ImportReportDto> ImportAsync(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".csv"  => _csv.ImportAsync(path),
            ".xlsx" => _xlsx.ImportAsync(path),
            _       => _csv.ImportAsync(path) // по умолчанию пробуем CSV
        };
    }
}
