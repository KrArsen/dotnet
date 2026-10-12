using System.Data;

namespace Sales.Dal.Options;

public class DalOptions
{
    public const string SectionName = "Dal";

    public int CommandTimeoutSeconds { get; set; } = 30;

    public IsolationLevel DefaultIsolationLevel { get; set; } = IsolationLevel.ReadCommitted;
}
