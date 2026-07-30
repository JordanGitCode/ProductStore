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
    // Price suggestion is a separate, manually-triggered workload; it never touches Price.
    public PriceStatus PriceStatus { get; set; }
    public decimal? SuggestedPrice { get; set; }
    public ICollection<PriceComparable> PriceComparables {get; set;} = new List<PriceComparable>();
    public ProductCategory ProductCategory {get; set;} = null!;
}

public enum ScanStatus
{
    Pending,
    Completed,
    Failed
}

public enum PriceStatus
{
    // Default: price suggestion only runs when the user asks for it, so nothing
    // auto-runs or gets requeued until a request moves a product to Pending.
    NotRequested,
    Pending,
    Completed,
    Failed
}