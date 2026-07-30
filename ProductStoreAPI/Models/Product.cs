namespace ProductStoreAPI.Models;

public class Product
{
    public Guid Id {get; set;}
    public Guid ProductCategoryId {get; set;}
    public required string Name {get; set;}
    public string? Description {get; set;}
    public decimal Price {get; set;}
    public ScanStatus ScanStatus { get; set; }
    public string? SuggestedName { get; set; }
    public string? SuggestedDescription { get; set; }
    public string? SuggestedCategory { get; set; }    
    public ProductCategory ProductCategory {get; set;} = null!;
}

public enum ScanStatus
{
    Pending,
    Completed,
    Failed
}