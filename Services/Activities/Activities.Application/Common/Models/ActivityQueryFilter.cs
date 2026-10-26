namespace Activities.Application.Common.Models;

public record ActivityQueryFilter(
    long? ClientId = null,
    string? Type = null,
    long? UserId = null,
    string? SearchText = null,
    long? ProductId = null);
