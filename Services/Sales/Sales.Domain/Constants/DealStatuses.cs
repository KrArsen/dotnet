namespace Sales.Domain.Constants;

public static class DealStatuses
{
    public const string Lead = "Lead";
    public const string Qualified = "Qualified";
    public const string ProposalSent = "ProposalSent";
    public const string Negotiation = "Negotiation";
    public const string Won = "Won";
    public const string Lost = "Lost";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Lead,
        Qualified,
        ProposalSent,
        Negotiation,
        Won,
        Lost
    };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status);

    public static bool IsClosed(string? status) =>
        string.Equals(status, Won, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, Lost, StringComparison.OrdinalIgnoreCase);
}
