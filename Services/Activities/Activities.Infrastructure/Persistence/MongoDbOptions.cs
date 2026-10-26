namespace Activities.Infrastructure.Persistence;

public class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "ActivitiesDb";
    public bool SeedEnabled { get; set; } = true;
    public int ServerSelectionTimeoutMS { get; set; } = 5000;
    public int MaxPoolSize { get; set; } = 100;
}
