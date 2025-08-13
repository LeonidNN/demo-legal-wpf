using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DemoLegal.Application.Abstractions;

/// <summary>
/// Оркестратор, который после импорта пробегает по ЛС и создаёт/обновляет дела
/// на основании последнего периода каждого ЛС.
/// </summary>
public interface IAfterImportCaseBuilder
{
    /// <summary>Обработать все аккаунты, для которых есть хотя бы один период.</summary>
    Task<int> BuildCasesForAllAccountsAsync();

    /// <summary>Обработать конкретные аккаунты по их Id.</summary>
    Task<int> BuildCasesForAccountsAsync(IEnumerable<Guid> accountIds);
}
