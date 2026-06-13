namespace STI.City.Data.Geocoding;

public interface ICityCacheSchemaInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
