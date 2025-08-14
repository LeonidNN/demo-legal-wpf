namespace DemoLegal.Domain.Models;

public sealed class CaseFileSummary
{
    public int CaseId { get; set; }
    public string Ls { get; set; } = string.Empty;
    public string? Fio { get; set; }
    public string Address { get; set; } = string.Empty;
    public string DebtorType { get; set; } = "person";  // person|company
    public decimal DebtAmount { get; set; }
    public string Period { get; set; } = string.Empty;   // "MM.yyyyMM.yyyy"
    public string PremisesType { get; set; } = string.Empty;
    public string MgmtStatusText { get; set; } = string.Empty;
}
