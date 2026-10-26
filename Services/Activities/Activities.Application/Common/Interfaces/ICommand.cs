using MediatR;

namespace Activities.Application.Common.Interfaces;

/// <summary>
/// Маркерний інтерфейс для CQRS-команд без значення, що повертається
/// </summary>
public interface ICommand : IRequest
{
}

/// <summary>
/// Маркерний інтерфейс для CQRS-команд із поверненням результату TResponse
/// </summary>
public interface ICommand<out TResponse> : IRequest<TResponse>
{
}

/// <summary>
/// Маркерний інтерфейс для CQRS-запитів читання
/// </summary>
public interface IQuery<out TResponse> : IRequest<TResponse>
{
}
