namespace Sales.Domain.Models;

public class DealStatusHistoryEntry
{
    public long Id { get; set; }
    public long DealId { get; set; }
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
