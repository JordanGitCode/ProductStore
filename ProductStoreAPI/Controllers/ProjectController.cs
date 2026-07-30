using Microsoft.AspNetCore.Mvc;
using ProductStoreAPI.Contracts;
using ProductStoreAPI.Models;
using ProductStoreAPI.Services;

namespace ProductStoreAPI.Controllers;

[ApiController]
[Route("product")]
public class ProductController : ControllerBase
{
    private const string UncategorisedName = "Uncategorised";

    private readonly IProductService _products;
    private readonly ScanQueue _scanQueue;
    private readonly ILogger<ProductController> _log;

    public ProductController(IProductService products, ScanQueue scanQueue, ILogger<ProductController> log)
    {
        _products = products;
        _scanQueue = scanQueue;
        _log = log;
    }

    [HttpGet("no-images")]
    public async Task<IActionResult> GetProducts()
    {
        return Ok(await _products.GetProductsAsync());
    }

    [HttpGet]
    public async Task<IActionResult> GetProductsWithImage()
    {
        return Ok(await _products.GetProductsWithImage());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProduct(Guid id)
    {
        var product = await _products.GetProductByIdAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct([FromForm] IFormFileCollection photos)
    {
        if (photos is null || photos.Count == 0)
        {
            return BadRequest("At least one photo is required.");
        }

        // Photos are all the user provides — the scan suggests the rest, and they edit it afterwards.
        var category = await _products.GetOrCreateCategoryAsync(UncategorisedName);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            ProductCategoryId = category.Id,
            Name = "Untitled",
            Description = null,
            Price = 0,
            ScanStatus = ScanStatus.Pending
        };

        var images = new List<ProductImage>();
        foreach (var photo in photos)
        {
            using var buffer = new MemoryStream();
            await photo.CopyToAsync(buffer);
            images.Add(new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Content = buffer.ToArray(),
                ContentType = photo.ContentType,
            });
        }

        await _products.CreateProductAsync(product, images);
        await _scanQueue.EnqueueAsync(product.Id);

        // Return only the id: the Product entity's navigations cycle once EF fixup has wired them up.
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, new { product.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProduct update)
    {
        if (string.IsNullOrWhiteSpace(update.Name))
        {
            return BadRequest("Name is required.");
        }

        if (update.Price < 0)
        {
            return BadRequest("Price cannot be negative.");
        }

        if (!await _products.CategoryExistsAsync(update.ProductCategoryId))
        {
            return BadRequest($"Product category '{update.ProductCategoryId}' does not exist.");
        }

        return await _products.UpdateProductAsync(id, update) ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/image")]
    public async Task<IActionResult> GetImageIds(Guid id)
    {
        return Ok(await _products.GetImageIdsAsync(id));
    }

    // max is the longest edge in pixels. Phone photos are multi-megabyte, which is far
    // more than a catalog thumbnail needs; ToJpeg never upscales, so an oversized max
    // simply returns the original dimensions.
    [HttpGet("{id:guid}/image/{imageId:guid}")]
    public async Task<IActionResult> GetImage(Guid id, Guid imageId, [FromQuery] int? max)
    {
        var image = await _products.GetImageAsync(id, imageId);
        if (image is null)
        {
            return NotFound();
        }

        // An image id always maps to the same bytes, so let the browser cache it
        // indefinitely. Each max value is a separate URL, so it caches separately.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";

        if (max is null)
        {
            return File(image.Content, image.ContentType);
        }

        try
        {
            var resized = ScanImageResizer.ToJpeg(image.Content, Math.Clamp(max.Value, 32, 2000), 80);
            return File(resized, "image/jpeg");
        }
        catch (Exception ex)
        {
            // A photo we can't decode shouldn't blank the catalog — serve the original.
            _log.LogWarning(ex, "Could not resize image {ImageId}, serving original", imageId);
            return File(image.Content, image.ContentType);
        }
    }

    [HttpGet("images/{id:guid}")]
    public async Task<ActionResult<IReadOnlyList<ProductImage>>> GetImages(Guid id)
    {
        IReadOnlyList<ProductImage> images = await _products.GetImagesAsync(id);
        return Ok(images);
    }

    [HttpPost("{id:guid}/scan")]
    public async Task<IActionResult> ScanProduct(Guid id)
    {
        if (await _products.GetProductByIdAsync(id) is null)
            return NotFound();

        if (!(await _products.GetImageIdsAsync(id)).Any())
            return BadRequest("Product has no photos to scan.");

        await _products.MarkScanPendingAsync(id);
        await _scanQueue.EnqueueAsync(id);

        return Accepted();
    }

    [HttpGet("{id:guid}/scan")]
    public async Task<IActionResult> GetScanStatus(Guid id)
    {
        var product = await _products.GetProductByIdAsync(id);
        return product is null ? NotFound() : Ok(new
        {
            product.ScanStatus,
            product.SuggestedName,
            product.SuggestedDescription,
            product.SuggestedCategory,
        });
    }
}
