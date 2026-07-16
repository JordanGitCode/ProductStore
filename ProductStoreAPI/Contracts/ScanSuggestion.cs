namespace ProductStoreAPI.Contracts;

// Price is deliberately absent: it cannot be derived from a photo.
public record ScanSuggestion(string? Name, string? Description, string? Category);
