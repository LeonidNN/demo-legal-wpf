using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DemoLegal.Application.DTOs;

namespace DemoLegal.Application.Abstractions;

/// <summary>Простые запросы по делам (для UI).</summary>
public interface ICaseQueries
{
    Task<IReadOnlyList<CaseFileDto>> GetRecentCasesAsync(int take = 100);
    Task<CaseFileDto?> GetByIdAsync(Guid caseId);
}
