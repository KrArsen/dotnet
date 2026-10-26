using Activities.Application.Common.Interfaces;

namespace Activities.Application.Activities.Commands.UpdateActivity;

/// <summary>
/// Команда оновлення активності/відгуку з перевіркою версії для оптимістичної конкурентності
/// </summary>
public record UpdateActivityCommand(
    string Id,
    string Text,
    int? Rating = null,
    string? Outcome = null,
    int ExpectedVersion = 1) : ICommand;
