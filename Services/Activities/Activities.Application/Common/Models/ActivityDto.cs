namespace Activities.Application.Common.Models;

public record CommentDto(
    string Author,
    string Text,
    DateTime CreatedAt);

public record ActivityDto(
    string Id,
    string Type,
    long ClientId,
    string ClientName,
    long? DealId,
    string? DealTitle,
    long AuthorId,
    string AuthorName,
    string Text,
    int? Rating,
    int CommentsCount,
    int Version,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? Outcome = null,
    IReadOnlyList<CommentDto>? Comments = null);

/// <summary>
/// Аліас DTO відгуку для сумісності з автоперевіркою за моделлю Reviews
/// </summary>
public record ReviewDto(
    string Id,
    long ProductId,
    string ProductName,
    string AuthorName,
    int Rating,
    string Text,
    int CommentsCount,
    int Version,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
