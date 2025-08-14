namespace DemoLegal.Domain.Models;

public sealed class PeriodBalance
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public DateOnly PeriodDate { get; set; }   // YYYY-MM-01
    public decimal DebtStart { get; set; }
    public decimal Accrued { get; set; }
    public decimal Paid { get; set; }
    public decimal DebtEnd { get; set; }
    public int? MonthsInDebt { get; set; }
    public string? DebtCategory { get; set; }
    public string? DebtStructure { get; set; }
    public string? SrcFile { get; set; }
    public string? RoomNo { get; set; }
}
