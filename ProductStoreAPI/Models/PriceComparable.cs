namespace ProductStoreAPI.Models;

// A similar listing found online, used to justify a suggested price.
public class PriceComparable
{
    public Guid Id {get; set;}
    public Guid ProductId {get; set;}
    public required string Title {get; set;}
    public decimal? Price {get; set;}
    public required string Url {get; set;}

    public Product Product {get; set;} = null!;
}
