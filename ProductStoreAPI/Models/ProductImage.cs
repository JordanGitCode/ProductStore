namespace ProductStoreAPI.Models;

public class ProductImage
{
    public Guid Id {get; set;}
    public Guid ProductId {get; set;}
    public required byte[] Content {get; set;}
    public required string ContentType {get; set;}

    public Product Product {get; set;} = null!;
}
