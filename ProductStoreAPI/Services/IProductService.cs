using Microsoft.AspNetCore.Mvc;
using ProductStoreAPI.Models;

namespace ProductStoreAPI.Services;

public interface IProductService
{
    Task<IEnumerable<Product>> GetProductsAsync();
    Task<Product> GetProductByIdAsync(Guid id);
}