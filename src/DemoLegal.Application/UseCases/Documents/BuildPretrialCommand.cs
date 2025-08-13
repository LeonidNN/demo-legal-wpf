using System;
using System.Threading.Tasks;
using DemoLegal.Application.Abstractions;
using DemoLegal.Application.DTOs;

namespace DemoLegal.Application.UseCases.Documents;

/// <summary>Команда: собрать досудебный пакет по делу.</summary>
public sealed class BuildPretrialCommand
{
    public Guid CaseId { get; }

    public BuildPretrialCommand(Guid caseId) => CaseId = caseId;
}

public sealed class BuildPretrialCommandHandler
{
    private readonly IDocumentService _docService;

    public BuildPretrialCommandHandler(IDocumentService docService)
    {
        _docService = docService;
    }

    public Task<DocPackageResultDto> HandleAsync(BuildPretrialCommand cmd)
        => _docService.BuildPretrialAsync(cmd.CaseId);
}
