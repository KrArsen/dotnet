using Activities.Application.Common.Interfaces;

namespace Activities.Application.Activities.Commands.DeleteActivity;

/// <summary>
/// Команда видалення активності/відгуку за Id
/// </summary>
public record DeleteActivityCommand(string Id) : ICommand;
