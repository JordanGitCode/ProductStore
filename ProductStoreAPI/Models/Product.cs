namespace ProductStoreAPI.Models;

public class Product
{
    public Guid Id {get; set;}
    public Guid ProductCategoryId {get; set;}
    public required string Name {get; set;}
    public string? Description {get; set;}
    public decimal Price {get; set;}
    
    public ProductCategory ProductCategory {get; set;} = null!;
}