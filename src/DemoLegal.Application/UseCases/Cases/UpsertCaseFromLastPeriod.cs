using System.Threading.Tasks;
using DemoLegal.Application.Abstractions;
using DemoLegal.Application.DTOs;

namespace DemoLegal.Application.UseCases.Cases;

/// <summary>
/// Команда: создать/обновить дело на основании учётной записи (ЛС) и её последнего периода.
/// Предполагается, что слой Infrastructure предоставит AccountDto и последний PeriodBalanceDto.
/// </summary>
public sealed class UpsertCaseFromLastPeriodCommand
{
    public AccountDto Account { get; }
    public PeriodBalanceDto LastPeriod { get; }

    public UpsertCaseFromLastPeriodCommand(AccountDto account, PeriodBalanceDto lastPeriod)
    {
        Account = account;
        LastPeriod = lastPeriod;
    }
}

public sealed class UpsertCaseFromLastPeriodHandler
{
    private readonly ICaseService _caseService;

    public UpsertCaseFromLastPeriodHandler(ICaseService caseService)
    {
        _caseService = caseService;
    }

    public Task<CaseFileDto> HandleAsync(UpsertCaseFromLastPeriodCommand cmd)
        => _caseService.UpsertCaseAsync(cmd.Account, cmd.LastPeriod);
}
