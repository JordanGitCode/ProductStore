using ProductStoreAPI.Contracts;
using ProductStoreAPI.Models;

namespace ProductStoreAPI.Services;

public interface IProductService
{
    Task<IEnumerable<Product>> GetProductsAsync();
    Task<Product?> GetProductByIdAsync(Guid id);
    Task<List<FullProduct>> GetProductsWithImage();
    Task<IEnumerable<ProductCategory>> GetCategoriesAsync();
    Task<ProductCategory> GetOrCreateCategoryAsync(string name);
    Task<bool> CategoryExistsAsync(Guid categoryId);
    Task<ProductCategory> GetProductCategoryAsync(Guid productId);
    Task<Product> CreateProductAsync(Product product, IReadOnlyList<ProductImage> images);
    Task<bool> UpdateProductAsync(Guid id, UpdateProduct update);
    Task<IEnumerable<Guid>> GetImageIdsAsync(Guid productId);
    Task<IReadOnlyList<ProductImage>> GetImagesAsync(Guid productId);
    Task<ProductImage?> GetImageAsync(Guid productId, Guid imageId);
    Task MarkScanPendingAsync(Guid productId);
}