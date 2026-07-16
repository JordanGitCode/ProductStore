using Microsoft.AspNetCore.Mvc;

namespace ProductStoreAPI.Controllers;

[ApiController]
[Route("product")]
public class ProductController : ControllerBase
{
    
    [HttpGet]
    public IActionResult getProducts()
    {
        
        return Ok();
    }

}