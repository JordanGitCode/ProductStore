using Microsoft.EntityFrameworkCore;
using ProductStoreAPI.Contracts;
using ProductStoreAPI.Data;
using ProductStoreAPI.Models;

namespace ProductStoreAPI.Services;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _db;

    public ProductService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Product>> GetProductsAsync()
    {
        return await _db.Products.ToListAsync();
    }

    public async Task<Product?> GetProductByIdAsync(Guid id)
    {
        return await _db.Products.FindAsync(id);
    }

    public async Task<List<FullProduct>> GetProductsWithImage()
    {
        // One query: the category joins in, and the first image id comes from a correlated subquery.
        return await _db.Products
            .AsNoTracking()
            .Select(p => new FullProduct
            {
                Id = p.Id,
                ProductCategoryId = p.ProductCategoryId,
                CategoryName = p.ProductCategory.Name,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                ImageId = _db.ProductImages
                    .Where(i => i.ProductId == p.Id)
                    .Select(i => (Guid?)i.Id)
                    .FirstOrDefault(),
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<ProductCategory>> GetCategoriesAsync()
    {
        return await _db.ProductCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<ProductCategory> GetOrCreateCategoryAsync(string name)
    {
        var trimmed = name.Trim();

        var existing = await _db.ProductCategories.FirstOrDefaultAsync(c => c.Name == trimmed);
        if (existing is not null)
        {
            return existing;
        }

        var category = new ProductCategory { Id = Guid.NewGuid(), Name = trimmed };
        _db.ProductCategories.Add(category);
        await _db.SaveChangesAsync();
        return category;
    }

    public async Task<bool> CategoryExistsAsync(Guid categoryId)
    {
        return await _db.ProductCategories.AnyAsync(c => c.Id == categoryId);
    }

    public async Task<ProductCategory> GetProductCategoryAsync(Guid categoryId)
    {
        return await _db.ProductCategories.FirstOrDefaultAsync(c => c.Id == categoryId);
    }

    public async Task<Product> CreateProductAsync(Product product, IReadOnlyList<ProductImage> images)
    {
        _db.Products.Add(product);
        _db.ProductImages.AddRange(images);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<bool> UpdateProductAsync(Guid id, UpdateProduct update)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null)
        {
            return false;
        }

        product.ProductCategoryId = update.ProductCategoryId;
        product.Name = update.Name;
        product.Description = update.Description;
        product.Price = update.Price;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<Guid>> GetImageIdsAsync(Guid productId)
    {
        return await _db.ProductImages
            .Where(i => i.ProductId == productId)
            .Select(i => i.Id)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ProductImage>> GetImagesAsync(Guid productId)
    {
        return await _db.ProductImages
            .AsNoTracking()
            .Where(i => i.ProductId == productId)
            .ToListAsync();
    }

    public async Task<ProductImage?> GetImageAsync(Guid productId, Guid imageId)
    {
        return await _db.ProductImages
            .SingleOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId);
    }

    public async Task<ProductImage?> GetFirstImageAsync(Guid productId)
    {
        return await _db.ProductImages
            .AsNoTracking()
            .Where(i => i.ProductId == productId)
            .FirstOrDefaultAsync();
    }

    public async Task MarkScanPendingAsync(Guid productId)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product is null) return;
        product.ScanStatus = ScanStatus.Pending;
        await _db.SaveChangesAsync();
    }
}
