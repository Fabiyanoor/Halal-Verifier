using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Halal_project_BL.Services;
using HalalProject.Model.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using HalalProject.Model.Entites;
using HalalProject.Database.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class IngredientController : ControllerBase
    {
        private readonly IIngredientService _ingredientService;
        private readonly IProductService _productService;
        private readonly AppDbContext _dbContext;
        private readonly IDistributedCache _cache;
        private readonly ILogger<IngredientController> _logger;

        public IngredientController(
            IIngredientService ingredientService,
            IProductService productService,
            AppDbContext dbContext,
            IDistributedCache cache,
            ILogger<IngredientController> logger)
        {
            _ingredientService = ingredientService ?? throw new ArgumentNullException(nameof(ingredientService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("get-ingredients")]
        public async Task<IActionResult> GetIngredients([FromBody] ProductRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for get-ingredients request: {ModelState}", ModelState);
                    return BadRequest(ModelState);
                }

                var response = await _ingredientService.GetIngredientsAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ingredients for product: {ProductName}", request.ProductName);
                return StatusCode(500, new { success = false, errorMessage = $"Error fetching ingredients: {ex.Message}", data = (object)null });
            }
        }

        [HttpPost("evaluate-ingredients")]
        public async Task<IActionResult> EvaluateIngredients([FromBody] IngredientListRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for evaluate-ingredients request: {ModelState}", ModelState);
                    return BadRequest(ModelState);
                }

                var response = await _ingredientService.GetIngredientStatusesAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating ingredients");
                return StatusCode(500, new { success = false, errorMessage = $"Error evaluating ingredients: {ex.Message}", data = (object)null });
            }
        }

        [HttpGet("evaluate-product/{productId}")]
        public async Task<IActionResult> EvaluateProduct(Guid productId)
        {
            try
            {
                _logger.LogInformation("Evaluating product with ID: {ProductId}", productId);
                var product = await _productService.GetProduct(productId);
                if (product == null)
                {
                    _logger.LogWarning("Product with ID {ProductId} not found.", productId);
                    return NotFound(new { success = false, errorMessage = $"Product with ID {productId} not found.", data = (object)null });
                }

                // Fetch linked ingredients
                var ingredients = await GetProductIngredients(productId);

                // If no linked ingredients, fetch via AI and link them
                if (!ingredients.Any())
                {
                    _logger.LogWarning("No ingredients found for product ID: {ProductId}, fetching via AI", productId);
                    var ingredientRequest = new ProductRequest
                    {
                        ProductName = product.ProductName,
                        Barcode = product.Barcode,
                        Country = product.Country
                    };
                    var ingredientResponse = await _ingredientService.GetIngredientsAsync(ingredientRequest);
                    ingredients = ingredientResponse.Ingredients;

                    if (ingredients.Any())
                    {
                        // Link AI-fetched ingredients to product
                        var linkMethod = _productService.GetType().GetMethod("LinkIngredientsToProduct", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        await (Task)linkMethod.Invoke(_productService, new object[] { product.ID, ingredients });
                        _logger.LogInformation("Linked {Count} ingredients to product ID: {ProductId} via AI: {Ingredients}",
                            ingredients.Count, productId, string.Join(", ", ingredients.Select(i => i.Name)));
                    }
                    else
                    {
                        _logger.LogWarning("No ingredients fetched via AI for product ID: {ProductId}", productId);
                    }
                }

                // Log ingredients used for status evaluation
                _logger.LogInformation("Ingredients for status evaluation: {Ingredients}",
                    ingredients.Any() ? string.Join(", ", ingredients.Select(i => $"{i.Name} ({i.Status})")) : "None");

                // Evaluate status
                product.Status = EvaluateIngredientStatuses(ingredients);
                _logger.LogInformation("Evaluated product status: {Status} for product ID: {ProductId}", product.Status, productId);

                // Update product status in database
                await _productService.UpdateProduct(product.ID, product, null, product.Barcode, false);

                // Clear cache to reflect updated product
                await ClearCache();
                _logger.LogInformation("Cleared cache after updating product ID: {ProductId}", productId);

                // Return response
                var response = new
                {
                    Id = product.ID,
                    ProductName = product.ProductName,
                    Status = product.Status,
                    Country = product.Country,
                    Ingredients = ingredients
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating product with ID {ProductId}: {Message}", productId, ex.Message);
                return StatusCode(500, new { success = false, errorMessage = $"Error evaluating product: {ex.Message}", data = (object)null });
            }
        }

        private async Task<List<Ingredient>> GetProductIngredients(Guid productId)
        {
            try
            {
                // Fetch product with ingredients
                var product = await _dbContext.Products
                    .Include(p => p.ProductIngredients)
                    .ThenInclude(pi => pi.Ingredient)
                    .FirstOrDefaultAsync(p => p.ID == productId);

                if (product?.ProductIngredients == null)
                {
                    _logger.LogWarning("No ingredients linked to product ID: {ProductId}", productId);
                    return new List<Ingredient>();
                }

                var ingredients = product.ProductIngredients
                    .Select(pi => pi.Ingredient)
                    .Where(i => i != null)
                    .ToList();

                _logger.LogInformation("Fetched {Count} linked ingredients for product ID: {ProductId}: {Ingredients}",
                    ingredients.Count, productId, string.Join(", ", ingredients.Select(i => i.Name)));
                return ingredients;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ingredients for product ID: {ProductId}", productId);
                return new List<Ingredient>();
            }
        }

        private string EvaluateIngredientStatuses(List<Ingredient> ingredients)
        {
            _logger.LogInformation("Evaluating {Count} ingredients for product status", ingredients.Count);
            if (!ingredients.Any())
            {
                _logger.LogWarning("No ingredients provided, setting status to Unknown");
                return "Unknown";
            }
            if (ingredients.Any(i => i.Status != null && i.Status.Trim().Equals("Haram", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation("Found Haram ingredient, setting status to Haram");
                return "Haram";
            }
            else if (ingredients.Any(i => i.Status != null && i.Status.Trim().Equals("Mushbooh", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation("Found Mushbooh ingredient, setting status to Mushbooh");
                return "Mushbooh";
            }
            else if (ingredients.All(i => i.Status != null && i.Status.Trim().Equals("Halal", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation("All ingredients are Halal, setting status to Halal");
                return "Halal";
            }
            else
            {
                _logger.LogWarning("Unexpected ingredient statuses, setting status to Unknown");
                return "Unknown";
            }
        }

        private async Task ClearCache()
        {
            try
            {
                _logger.LogInformation("Clearing cache for key 'products_all'");
                await _cache.RemoveAsync("products_all");
                _logger.LogInformation("Successfully cleared cache");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear cache: {Message}", ex.Message);
            }
        }
    }
}