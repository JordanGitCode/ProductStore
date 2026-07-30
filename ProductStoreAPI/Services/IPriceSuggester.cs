using ProductStoreAPI.Contracts;

namespace ProductStoreAPI.Services;

public interface IPriceSuggester
{
    // query identifies the item (from the scan's suggested name); category narrows the search.
    Task<PriceSuggestion> SuggestPriceAsync(string query, string? category, CancellationToken ct);
}
