using ProductStoreAPI.Contracts;

namespace ProductStoreAPI.Services;

public interface IProductScanner
{
    Task<ScanSuggestion> ScanAsync(IReadOnlyList<ScanImage> images, CancellationToken ct);
}
