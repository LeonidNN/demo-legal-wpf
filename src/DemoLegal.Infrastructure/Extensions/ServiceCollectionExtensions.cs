using DemoLegal.Application.Abstractions;
using DemoLegal.Infrastructure.Documents;
using DemoLegal.Infrastructure.Import;
using DemoLegal.Infrastructure.Persistence;
using DemoLegal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace DemoLegal.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрация инфраструктуры: DbContext (SQLite), Importers (CSV/XLSX), CaseService, DocumentService, Queries.
    /// </summary>
    public static IServiceCollection AddDemoLegalInfrastructure(this IServiceCollection services, string? dbPath = null)
    {
        var options = SqliteFactory.CreateOptions(dbPath);
        services.AddDbContext<DemoContext>(o =>
        {
            var cs = options.Extensions
                .OfType<Microsoft.EntityFrameworkCore.Sqlite.Infrastructure.Internal.SqliteOptionsExtension>()
                .First().ConnectionString;
            o.UseSqlite(cs);
        });

        // Импортёры
        services.AddScoped<CsvImporter>();
        services.AddScoped<XlsxImporter>();
        services.AddScoped<IImporter, CompositeImporter>();   // IImporter  Composite (сам выбирает)

        // Бизнес-сервисы
        services.AddScoped<ICaseService, CaseService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ICaseQueries, CaseQueries>();
        services.AddScoped<IAfterImportCaseBuilder, AfterImportCaseBuilder>();

        return services;
    }
}
