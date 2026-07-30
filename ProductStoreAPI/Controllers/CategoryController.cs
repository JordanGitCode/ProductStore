using Microsoft.AspNetCore.Mvc;
using ProductStoreAPI.Contracts;
using ProductStoreAPI.Services;

namespace ProductStoreAPI.Controllers;

[ApiController]
[Route("category")]
public class CategoryController : ControllerBase
{
    private readonly IProductService _products;

    public CategoryController(IProductService products)
    {
        _products = products;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        return Ok(await _products.GetCategoriesAsync());
    }

    // Get-or-create: accepting the same suggested category twice must not duplicate it.
    [HttpPost]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategory request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        var category = await _products.GetOrCreateCategoryAsync(request.Name);

        // Projection, not the entity: ProductCategory.Products would cycle.
        return Ok(new { category.Id, category.Name, category.Description });
    }
}
