namespace ProductStoreAPI.Contracts;

public record UpdateProduct(Guid ProductCategoryId, string Name, string? Description, decimal Price);
