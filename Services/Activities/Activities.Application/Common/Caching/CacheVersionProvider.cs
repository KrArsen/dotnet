namespace Activities.Application.Common.Caching;

/// <summary>
/// Інтерфейс провайдера версії кешу для миттєвої інвалідації при виконанні мутуючих команд
/// </summary>
public interface ICacheVersionProvider
{
    long CurrentVersion { get; }
    void Invalidate();
}

/// <summary>
/// Потокобезпечний провайдер версії кешу в оперативній пам'яті
/// </summary>
public class CacheVersionProvider : ICacheVersionProvider
{
    private long _version = 1;

    public long CurrentVersion => Interlocked.Read(ref _version);

    public void Invalidate()
    {
        Interlocked.Increment(ref _version);
    }
}
