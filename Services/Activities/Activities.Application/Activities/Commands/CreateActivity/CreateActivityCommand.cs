using Activities.Application.Common.Interfaces;

namespace Activities.Application.Activities.Commands.CreateActivity;

/// <summary>
/// Команда створення нової активності або відгуку
/// </summary>
public record CreateActivityCommand(
    string Type,
    long ClientId,
    string ClientName,
    long AuthorUserId,
    string AuthorDisplayName,
    string Text,
    int? Rating = null,
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
    List<long>? ProductIds = null) : ICommand<string>;
