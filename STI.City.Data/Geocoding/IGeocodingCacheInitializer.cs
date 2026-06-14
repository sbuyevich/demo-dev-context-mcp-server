namespace STI.City.Data;

public interface IGeocodingCacheInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
