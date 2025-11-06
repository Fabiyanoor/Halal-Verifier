using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HalalProject.Model.Entites;
using Newtonsoft.Json;
using Microsoft.Extensions.Caching.Distributed;
using Halal_project_BL.Repositories;
using HalalProject.Database.Data;
using Microsoft.EntityFrameworkCore;
using Halal_project_BL.Services;
using HalalProject.Model.Models;
using Microsoft.Extensions.Logging;
using HalalProject.Model.DTO;

namespace Halal_project_BL.Services
{
    public interface IProductService
    {
        Task<Product> CreateProduct(Product product, List<string> ingredientNames, string barcode, bool useAI);
        Task<Product> GetProduct(Guid id);
        Task<List<Product>> GetProducts();
        Task UpdateProduct(Guid id, Product updatedProduct, List<string> ingredientNames, string barcode, bool useAI);
        Task DeleteProduct(Guid id);
        Task<bool> ProductExists(Guid id);
        Task<List<string>> GetCategories();
        Task<List<string>> GetCountries();
        Task<List<Product>> GetProductsByName(string name);
        Task<Product> CreateProductWithImageUrl(
            string productName,
            string country,
            string description,
            string category,
            string barcode,
            string status,
            string imageUrl,
            List<IngredientDto> ingredients);
            Task<List<Product>> GetProductsFiltered(string name, List<string> categories, string country, string status);
    }

    public class ProductService : IProductService
    {
        private readonly AppDbContext _dbContext;
        private readonly IProductRepository _repository;
        private readonly IIngredientService _ingredientService;
        private readonly IDistributedCache _cache;
        private readonly ILogger<ProductService> _logger;

        public ProductService(
            IProductRepository repository,
            IIngredientService ingredientService,
            IDistributedCache cache,
            AppDbContext dbContext,
            ILogger<ProductService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _ingredientService = ingredientService ?? throw new ArgumentNullException(nameof(ingredientService));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Product> CreateProduct(Product product, List<string> ingredientNames, string barcode, bool useAI)
        {
            _logger.LogInformation("Creating product: {ProductName}, UseAI: {UseAI}, Barcode: {Barcode}, IngredientNames: {IngredientNames}",
                product.ProductName, useAI, barcode, ingredientNames != null ? string.Join(",", ingredientNames) : "null");
            product.ID = Guid.NewGuid();
            product.CreatedAt = DateTime.UtcNow;
            product.UpdatedAt = DateTime.UtcNow;

            if (string.IsNullOrEmpty(product.ProductName) || string.IsNullOrEmpty(product.Country) || string.IsNullOrEmpty(product.Description))
            {
                _logger.LogWarning("Validation failed: Product name, country, and description are required.");
                throw new ArgumentException("Product name, country, and description are required.");
            }
            if (string.IsNullOrEmpty(product.Category))
            {
                _logger.LogWarning("Validation failed: Category is required.");
                throw new ArgumentException("Category is required.");
            }
            if (useAI && string.IsNullOrEmpty(barcode))
            {
                _logger.LogWarning("Validation failed: Barcode is required for AI ingredient generation.");
                throw new ArgumentException("Barcode is required for AI ingredient generation.");
            }

            try
            {
                var createdProduct = await _repository.CreateProduct(product);

                List<Ingredient> ingredients = new();
                if (useAI)
                {
                    _logger.LogInformation("Fetching AI-generated ingredients for barcode: {Barcode}, country: {Country}", barcode, product.Country);
                    try
                    {
                        var request = new ProductRequest { ProductName = product.ProductName, Barcode = barcode, Country = product.Country };
                        var response = await _ingredientService.GetIngredientsAsync(request);
                        ingredients = response.Ingredients;
                        _logger.LogInformation("Retrieved {Count} AI-generated ingredients: {Ingredients}",
                            ingredients.Count, ingredients.Any() ? string.Join(",", ingredients.Select(i => i.Name)) : "none");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch AI ingredients: {Message}, proceeding without ingredients", ex.Message);
                    }
                }
                else if (ingredientNames != null && ingredientNames.Any())
                {
                    _logger.LogInformation("Processing manual ingredient names: {IngredientNames}", string.Join(",", ingredientNames));
                    ingredients = await ProcessIngredientNames(ingredientNames, product.Country);
                    _logger.LogInformation("Processed {Count} manual ingredients: {Ingredients}",
                        ingredients.Count, ingredients.Any() ? string.Join(",", ingredients.Select(i => i.Name)) : "none");
                }
                else
                {
                    _logger.LogWarning("No ingredients provided for product: {ProductName}", product.ProductName);
                }

                if (ingredients.Any())
                {
                    await LinkIngredientsToProduct(createdProduct.ID, ingredients);
                }
                else
                {
                    _logger.LogWarning("No ingredients linked to product: {ProductName}", createdProduct.ProductName);
                }

                createdProduct.Status = EvaluateIngredientStatuses(ingredients);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Product status set to {Status} for product ID {ProductId}", createdProduct.Status, createdProduct.ID);

                await ClearCache();
                _logger.LogInformation("Successfully created product with ID {Id}", createdProduct.ID);
                return createdProduct;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product: {ProductName}", product.ProductName);
                throw new Exception($"Failed to create product: {ex.Message}", ex);
            }
        }

        public async Task DeleteProduct(Guid id)
        {
            _logger.LogInformation("Deleting product with ID {Id}", id);
            var product = await _repository.GetProduct(id);
            if (product == null)
            {
                _logger.LogWarning("Product with ID {Id} not found.", id);
                throw new ArgumentException($"Product with ID {id} not found.");
            }

            await _repository.DeleteProduct(id);
            await ClearCache();
            _logger.LogInformation("Successfully deleted product with ID {Id}", id);
        }

        public async Task<Product> GetProduct(Guid id)
        {
            _logger.LogInformation("Fetching product with ID {Id}", id);
            var product = await _repository.GetProduct(id);
            if (product == null)
            {
                _logger.LogWarning("Product with ID {Id} not found", id);
                throw new ArgumentException($"Product with ID {id} not found.");
            }
            _logger.LogInformation("Successfully fetched product with ID {Id}", id);
            return product;
        }

        public async Task<List<Product>> GetProducts()
        {
            _logger.LogInformation("Fetching all products");
            try
            {
                var products = await _repository.GetProducts();
                _logger.LogInformation("Fetched {Count} products from database", products.Count);
                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching products: {Message}", ex.Message);
                throw new Exception($"Failed to fetch products: {ex.Message}", ex);
            }
        }
        public async Task UpdateProduct(Guid id, Product updatedProduct, List<string> ingredientNames, string barcode, bool useAI)
        {
            _logger.LogInformation("Updating product with ID {Id}, ProductName: {ProductName}, UseAI: {UseAI}, Barcode: {Barcode}, IngredientNames: {IngredientNames}",
                id, updatedProduct.ProductName, useAI, barcode, ingredientNames != null ? string.Join(",", ingredientNames) : "null");

            var existingProduct = await _dbContext.Products
                .Include(p => p.ProductIngredients)
                .ThenInclude(pi => pi.Ingredient)
                .FirstOrDefaultAsync(p => p.ID == id);

            if (existingProduct == null)
            {
                _logger.LogWarning("Product with ID {Id} not found.", id);
                throw new ArgumentException($"Product with ID {id} not found.");
            }

            if (string.IsNullOrEmpty(updatedProduct.ProductName) || string.IsNullOrEmpty(updatedProduct.Country) || string.IsNullOrEmpty(updatedProduct.Description))
            {
                _logger.LogWarning("Validation failed: Product name, country, and description are required.");
                throw new ArgumentException("Product name, country, and description are required.");
            }
            if (string.IsNullOrWhiteSpace(updatedProduct.Category) || updatedProduct.Category == "string")
            {
                _logger.LogInformation("No valid Category provided, retaining existing Category: {ExistingCategory}", existingProduct.Category);
                updatedProduct.Category = existingProduct.Category;
            }
            if (useAI && string.IsNullOrEmpty(barcode))
            {
                _logger.LogWarning("Validation failed: Barcode is required for AI ingredient generation.");
                throw new ArgumentException("Barcode is required for AI ingredient generation.");
            }

            try
            {
                existingProduct.ProductName = updatedProduct.ProductName;
                existingProduct.Country = updatedProduct.Country;
                existingProduct.Description = updatedProduct.Description;
                existingProduct.Barcode = barcode ?? existingProduct.Barcode;
                existingProduct.Category = updatedProduct.Category;
                existingProduct.ImageUrl = updatedProduct.ImageUrl ?? existingProduct.ImageUrl;
                existingProduct.Status = updatedProduct.Status ?? existingProduct.Status;
                existingProduct.UpdatedAt = DateTime.UtcNow;

                // Only fetch and link ingredients if useAI is true or ingredientNames are provided
                if (useAI || (ingredientNames != null && ingredientNames.Any()))
                {
                    await _repository.RemoveProductIngredients(id);

                    List<Ingredient> ingredients = new();
                    if (useAI)
                    {
                        _logger.LogInformation("Fetching AI-generated ingredients for barcode: {Barcode}, country: {Country}", barcode, updatedProduct.Country);
                        try
                        {
                            var request = new ProductRequest { ProductName = updatedProduct.ProductName, Barcode = barcode, Country = updatedProduct.Country };
                            var response = await _ingredientService.GetIngredientsAsync(request);
                            ingredients = response.Ingredients;
                            _logger.LogInformation("Retrieved {Count} AI-generated ingredients: {Ingredients}",
                                ingredients.Count, ingredients.Any() ? string.Join(",", ingredients.Select(i => i.Name)) : "none");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to fetch AI ingredients: {Message}, proceeding without ingredients", ex.Message);
                        }
                    }
                    else if (ingredientNames != null && ingredientNames.Any())
                    {
                        _logger.LogInformation("Processing manual ingredient names: {IngredientNames}", string.Join(",", ingredientNames));
                        ingredients = await ProcessIngredientNames(ingredientNames, updatedProduct.Country);
                        _logger.LogInformation("Processed {Count} manual ingredients: {Ingredients}",
                            ingredients.Count, ingredients.Any() ? string.Join(",", ingredients.Select(i => i.Name)) : "none");
                    }

                    if (ingredients.Any())
                    {
                        await LinkIngredientsToProduct(id, ingredients);
                        existingProduct.Status = EvaluateIngredientStatuses(ingredients);
                    }
                    else
                    {
                        _logger.LogWarning("No ingredients linked to product: {ProductName}", updatedProduct.ProductName);
                        existingProduct.Status = "Unknown";
                    }
                }

                await _dbContext.SaveChangesAsync();
                await _repository.UpdateProduct(existingProduct);
                await ClearCache();
                _logger.LogInformation("Successfully updated product with ID {Id}, Status: {Status}", id, existingProduct.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product with ID {Id}", id);
                throw new Exception($"Failed to update product: {ex.Message}", ex);
            }
        }

        public async Task<bool> ProductExists(Guid id)
        {
            _logger.LogInformation("Checking if product with ID {Id} exists", id);
            var exists = await _repository.ProductExists(id);
            _logger.LogInformation("Product with ID {Id} {Exists}", id, exists ? "exists" : "does not exist");
            return exists;
        }

        public async Task<Product> CreateProductWithImageUrl(
       string productName,
 string country,
 string description,
 string category,
 string barcode,
 string imageUrl,   // 6th param
 string status,     // 7th param
 List<IngredientDto> ingredients)
        {
            _logger.LogInformation("Creating product with image URL: {ImageUrl}", imageUrl);

            var product = new Product
            {
                ID = Guid.NewGuid(),
                ProductName = productName,
                Country = country,
                Description = description,
                Category = category,
                Barcode = barcode,
                ImageUrl = imageUrl,
                Status = status,
                AddedBy = "Admin",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.CreateProduct(product);

            // Process ingredients
            foreach (var ingredientDto in ingredients)
            {
                // Check if ingredient already exists
                var existingIngredient = await _dbContext.Ingredients
                    .FirstOrDefaultAsync(i => i.Name.ToLower() == ingredientDto.Name.ToLower());

                Ingredient ingredient;

                if (existingIngredient != null)
                {
                    // Use existing ingredient
                    ingredient = existingIngredient;
                }
                else
                {
                    // Create new ingredient
                    ingredient = new Ingredient
                    {
                        Id = Guid.NewGuid(),
                        Name = ingredientDto.Name,
                        Status = ingredientDto.Status ?? "Mushbooh", // Default status
                        ECode = ingredientDto.ECode ?? "N/A",
                        Description = ingredientDto.Description ?? $"Ingredient for {productName}",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _repository.CreateIngredient(ingredient);
                }

                // Link ingredient to product
                var productIngredient = new ProductIngredient
                {
                    ProductId = product.ID,
                    IngredientId = ingredient.Id,
                    CreatedAt = DateTime.UtcNow
                };

                await _repository.CreateProductIngredient(productIngredient);
            }

            await ClearCache();
            return product;
        }
        public async Task<List<string>> GetCategories()
        {
            _logger.LogInformation("Fetching categories");
            var categories = await _repository.GetCategories();
            _logger.LogInformation("Successfully fetched {Count} categories", categories.Count);
            return categories;
        }

        public async Task<List<string>> GetCountries()
        {
            _logger.LogInformation("Fetching countries");
            var countries = await _repository.GetCountries();
            _logger.LogInformation("Successfully fetched {Count} countries", countries.Count);
            return countries;
        }

        public async Task<List<Product>> GetProductsByName(string name)
        {
            _logger.LogInformation("Searching products by name: {Name}", name);
            var products = await _repository.GetProductsByName(name);
            _logger.LogInformation("Found {Count} products for name: {Name}", products.Count, name);
            return products;
        }

        public async Task<List<Product>> GetProductsFiltered(string name, List<string> categories, string country, string status)
        {
            _logger.LogInformation("Filtering products with name: {Name}, categories: {Categories}, country: {Country}, status: {Status}",
                name, string.Join(",", categories), country, status);
            var products = await _repository.GetProductsFiltered(name, categories, country, status);
            _logger.LogInformation("Found {Count} products matching filter criteria", products.Count);
            return products;
        }

        private async Task<List<Ingredient>> ProcessIngredientNames(List<string> ingredientNames, string country)
        {
            _logger.LogInformation("Processing ingredient names: {IngredientNames}", string.Join(", ", ingredientNames));
            var ingredients = new List<Ingredient>();
            foreach (var name in ingredientNames)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    _logger.LogWarning("Skipping empty ingredient name");
                    continue;
                }

                var ingredient = await _dbContext.Ingredients
                    .FirstOrDefaultAsync(i => i.Name.ToLower() == name.ToLower());

                if (ingredient == null)
                {
                    ingredient = new Ingredient
                    {
                        Id = Guid.NewGuid(),
                        Name = name,
                        Status = "Mushbooh",
                        ECode = "N/A",
                        Description = name,
                        IsHalal = new List<string> { "None" },
                        IsHaram = new List<string> { "None" },
                        IsMushbooh = new List<string> { country },
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _dbContext.Ingredients.Add(ingredient);
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Created new ingredient: {Name} with ID: {Id}", name, ingredient.Id);
                }
                ingredients.Add(ingredient);
            }
            return ingredients;
        }

        private async Task LinkIngredientsToProduct(Guid productId, List<Ingredient> ingredients)
        {
            _logger.LogInformation("Linking {Count} ingredients to product ID: {ProductId}", ingredients.Count, productId);
            try
            {
                foreach (var ingredient in ingredients)
                {
                    var existingIngredient = await _dbContext.Ingredients
                        .FirstOrDefaultAsync(i => i.Id == ingredient.Id || i.Name.ToLower() == ingredient.Name.ToLower());

                    if (existingIngredient == null)
                    {
                        ingredient.Id = Guid.NewGuid();
                        ingredient.CreatedAt = DateTime.UtcNow;
                        ingredient.UpdatedAt = DateTime.UtcNow;
                        _dbContext.Ingredients.Add(ingredient);
                        existingIngredient = ingredient;
                        await _dbContext.SaveChangesAsync();
                        _logger.LogInformation("Added new ingredient: {Name} with ID: {Id}", ingredient.Name, ingredient.Id);
                    }

                    var productIngredient = new ProductIngredient
                    {
                        ProductId = productId,
                        IngredientId = existingIngredient.Id,
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.ProductIngredients.Add(productIngredient);
                    _logger.LogInformation("Added ProductIngredient link: ProductId={ProductId}, IngredientId={IngredientId}", productId, existingIngredient.Id);
                }
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Successfully linked {Count} ingredients to product ID: {ProductId}", ingredients.Count, productId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to link ingredients to product ID: {ProductId}", productId);
                throw new Exception($"Failed to link ingredients to product: {ex.Message}", ex);
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