using System.ComponentModel.DataAnnotations;

namespace Activities.WebApi.Contracts;

public record CreateActivityRequest(
    [Required] string Type,
    [Range(1, long.MaxValue)] long ClientId,
    [Required, StringLength(200)] string ClientName,
    [Range(1, long.MaxValue)] long AuthorUserId,
    [Required, StringLength(100)] string AuthorDisplayName,
    [Required, StringLength(5000)] string Text,
    [Range(1, 5)] int? Rating = null,
    long? DealId = null,
    string? DealTitle = null,
    string? Direction = null,
    int? DurationSec = null,
    string? Outcome = null,
    string? Subject = null,
    List<string>? Attachments = null,
    string? Location = null,
    List<string>? Participants = null,
    string? Agenda = null,
    DateTime? DueDate = null,
    List<long>? ProductIds = null);

public record UpdateActivityRequest(
    [Required, StringLength(5000)] string Text,
    [Range(1, 5)] int? Rating = null,
    string? Outcome = null,
    int? ExpectedVersion = null);

public record AddCommentRequest(
    [Required, StringLength(100)] string Author,
    [Required, StringLength(2000)] string Text);

public record CreateReviewRequest(
    [Range(1, long.MaxValue)] long ProductId,
    [Required, StringLength(200)] string ProductName,
    [Range(1, long.MaxValue)] long AuthorUserId,
    [Required, StringLength(100)] string AuthorDisplayName,
    [Required, StringLength(2000)] string Text,
    [Range(1, 5)] int Rating);

public record UpdateReviewRequest(
    [Required, StringLength(2000)] string Text,
    [Range(1, 5)] int Rating,
    int? ExpectedVersion = null);
