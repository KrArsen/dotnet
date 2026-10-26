using Activities.Application.Common.Interfaces;

namespace Activities.Application.Activities.Commands.AddComment;

/// <summary>
/// Команда додавання коментаря команди до активності
/// </summary>
public record AddCommentCommand(
    string ActivityId,
    string Author,
    string Text) : ICommand;
