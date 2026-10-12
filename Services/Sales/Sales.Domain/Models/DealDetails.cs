namespace Sales.Domain.Models;

public class DealDetails
{
    public long DealId { get; set; }
    public string Source { get; set; } = "Inbound";
    public string? Description { get; set; }
    public string? LostReason { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
