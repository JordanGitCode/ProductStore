namespace ProductStoreAPI.Contracts;

// Transport type from the price suggester. Advisory: it fills SuggestedPrice,
// never the user-entered Price. Comparables are the listings it based the price on.
public record PriceSuggestion(decimal? Price, IReadOnlyList<ComparableListing> Comparables);
