namespace DemoLegal.Domain.Models;

public sealed class Account
{
    public int Id { get; set; }
    public string Ls { get; set; } = string.Empty;
    public string? LsCode { get; set; }
    public string? Fio { get; set; }
    public string AddressRaw { get; set; } = string.Empty;
    public string? AddressNorm { get; set; }
    public string? PremisesType { get; set; }
    public string? LsStatus { get; set; }
    public DateTime? LsCloseDate { get; set; }
    public string? LsType { get; set; }
    public string? MgmtStatus { get; set; }
    public string Organization { get; set; } = string.Empty;
    public string? GroupCompany { get; set; }
    public string? Division { get; set; }
    public string? DivisionHead { get; set; }
    public string? AccrualCenter { get; set; }
    public string? ObjectName { get; set; }
    public string? District { get; set; }
    public string? House { get; set; }
    public string? AdrN { get; set; }
}
