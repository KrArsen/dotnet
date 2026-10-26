using Activities.Application.Common.Interfaces;

namespace Activities.Application.Reviews.Commands.CreateReview;

public record CreateReviewCommand(
    long ProductId,
    string ProductName,
    long AuthorUserId,
    string AuthorDisplayName,
    string Text,
    int Rating) : ICommand<string>;
