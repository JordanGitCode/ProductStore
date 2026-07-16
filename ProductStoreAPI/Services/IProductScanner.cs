using ProductStoreAPI.Contracts;

namespace ProductStoreAPI.Services;

public interface IProductScanner
{
    Task<ScanSuggestion> ScanAsync(IReadOnlyList<ProductImage> images, CancellationToken ct);
}
