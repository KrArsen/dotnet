using Activities.Domain.ValueObjects;
using MongoDB.Bson;

namespace Activities.Domain.Entities;

/// <summary>
/// Аліас/підклас для відгуків та клієнтських оцінок послуг каталогу
/// </summary>
public class Review : Activity
{
    private Review() : base()
    {
    }

    public Review(
        long clientId,
        string clientName,
        Author author,
        string text,
        Rating rating,
        long? dealId = null,
        string? dealTitle = null,
        ObjectId? discussionId = null,
        IEnumerable<RelatedProduct>? relatedProducts = null)
        : base(
            type: "note",
            clientId: clientId,
            clientName: clientName,
            author: author,
            text: text,
            rating: rating,
            dealId: dealId,
            dealTitle: dealTitle,
            discussionId: discussionId,
            relatedProducts: relatedProducts)
    {
    }
}
