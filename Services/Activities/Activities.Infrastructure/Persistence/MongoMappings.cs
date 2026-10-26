using Activities.Domain.Common;
using Activities.Domain.Entities;
using Activities.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Activities.Infrastructure.Persistence;

/// <summary>
/// Кастомний BSON серіалізатор для Rating, що серіалізує Value Object як звичайне BSON Int32 число
/// </summary>
public class RatingBsonSerializer : SerializerBase<Rating?>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Rating? value)
    {
        if (value == null)
        {
            context.Writer.WriteNull();
        }
        else
        {
            context.Writer.WriteInt32(value.Value);
        }
    }

    public override Rating? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        if (context.Reader.CurrentBsonType == BsonType.Null)
        {
            context.Reader.ReadNull();
            return null;
        }

        var intValue = context.Reader.ReadInt32();
        return new Rating(intValue);
    }
}

/// <summary>
/// Централізована реєстрація BSON ClassMap без жодних сторонніх BSON-атрибутів у шарі Domain
/// </summary>
public static class MongoMappings
{
    private static readonly object SyncLock = new();
    private static bool _isRegistered;

    public static void Register()
    {
        if (_isRegistered) return;

        lock (SyncLock)
        {
            if (_isRegistered) return;

            // 1. Налаштування конвенцій: camelCase для назв полів та ігнорування невідомих елементів
            var pack = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true)
            };
            ConventionRegistry.Register("camelCase", pack, _ => true);

            // 2. Реєстрація кастомного серіалізатора для Rating
            BsonSerializer.RegisterSerializer(new RatingBsonSerializer());

            // 3. Мапінг BaseEntity
            if (!BsonClassMap.IsClassMapRegistered(typeof(BaseEntity)))
            {
                BsonClassMap.RegisterClassMap<BaseEntity>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(c => c.Id);
                    cm.MapProperty(c => c.CreatedAt).SetElementName("createdAt");
                    cm.MapProperty(c => c.UpdatedAt).SetElementName("updatedAt");
                    cm.MapProperty(c => c.Version).SetElementName("version");
                });
            }

            // 4. Мапінг Value Objects через фабричні конструктори
            if (!BsonClassMap.IsClassMapRegistered(typeof(Author)))
            {
                BsonClassMap.RegisterClassMap<Author>(cm =>
                {
                    cm.AutoMap();
                    cm.MapCreator(a => new Author(a.UserId, a.DisplayName));
                    cm.MapProperty(a => a.UserId).SetElementName("userId");
                    cm.MapProperty(a => a.DisplayName).SetElementName("displayName");
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(RelatedProduct)))
            {
                BsonClassMap.RegisterClassMap<RelatedProduct>(cm =>
                {
                    cm.AutoMap();
                    cm.MapCreator(p => new RelatedProduct(p.ProductId, p.Name));
                    cm.MapProperty(p => p.ProductId).SetElementName("productId");
                    cm.MapProperty(p => p.Name).SetElementName("name");
                });
            }

            // 5. Мапінг Comment
            if (!BsonClassMap.IsClassMapRegistered(typeof(Comment)))
            {
                BsonClassMap.RegisterClassMap<Comment>(cm =>
                {
                    cm.AutoMap();
                    cm.MapProperty(c => c.Author).SetElementName("author");
                    cm.MapProperty(c => c.Text).SetElementName("text");
                    cm.MapProperty(c => c.CreatedAt).SetElementName("createdAt");
                });
            }

            // 6. Мапінг агрегату Activity (чистий BSON без внутрішніх префіксів .NET)
            if (!BsonClassMap.IsClassMapRegistered(typeof(Activity)))
            {
                BsonClassMap.RegisterClassMap<Activity>(cm =>
                {
                    cm.AutoMap();
                    cm.SetIgnoreExtraElements(true);
                    cm.SetDiscriminatorIsRequired(false);

                    // Мапінг приватних полів на відповідні назви колекцій з Лаб. 01
                    cm.MapField("_comments").SetElementName("comments");
                    cm.MapField("_relatedProducts").SetElementName("relatedProducts");
                    cm.MapField("_attachments").SetElementName("attachments");
                    cm.MapField("_participants").SetElementName("participants");

                    // Запобігаємо дублюванню гетерів IReadOnlyCollection
                    cm.UnmapProperty(a => a.Comments);
                    cm.UnmapProperty(a => a.RelatedProducts);
                    cm.UnmapProperty(a => a.Attachments);
                    cm.UnmapProperty(a => a.Participants);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(Review)))
            {
                BsonClassMap.RegisterClassMap<Review>(cm =>
                {
                    cm.AutoMap();
                    cm.SetIgnoreExtraElements(true);
                    cm.SetDiscriminatorIsRequired(false);
                });
            }

            _isRegistered = true;
        }
    }
}
