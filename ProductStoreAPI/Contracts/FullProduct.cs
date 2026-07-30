using ProductStoreAPI.Models;

namespace ProductStoreAPI.Contracts;

public class FullProduct
{
    public Guid Id {get; set;}
    public Guid ProductCategoryId {get; set;}
    public required string CategoryName {get; set;}
    public required string Name {get; set;}
    public string? Description {get; set;}
    public decimal Price {get; set;}
    // Just the id: the bytes are fetched separately from /product/{id}/image/{imageId},
    // so the catalog response stays small and the browser caches each image itself.
    public Guid? ImageId {get; set;}
}