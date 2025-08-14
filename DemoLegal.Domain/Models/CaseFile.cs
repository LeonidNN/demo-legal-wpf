namespace DemoLegal.Domain.Models;

public sealed class CaseFile
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "candidate"; // candidate|pretrial|court_order|lawsuit|fssp
    public string DebtorType { get; set; } = "person"; // person|company
    public decimal DebtAmount { get; set; }
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public string ServiceKind { get; set; } = "ЖКУ (обобщ.)";
    public string MgmtStatusText { get; set; } = string.Empty; // Человекочитаемая фраза для документов
    public string? EnrichmentFlagsJson { get; set; } // JSON с флагами добора (inn|birth_date|birth_place)
}
