using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Halal_project_BL.Repositories;
using HalalProject.Database.Data;
using HalalProject.Model.Entites;
using HalalProject.Model.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Halal_project_BL.Services
{
    public interface IIngredientService
    {
        Task<ProductResponse> GetIngredientsAsync(ProductRequest request);
        Task<IngredientListResponse> GetIngredientStatusesAsync(IngredientListRequest request);
        Task SaveIngredientsToDatabaseAsync(ProductResponse response);
        Task<ProductEvaluationResponse> EvaluateProductStatusAsync(Guid productId);
    }

    public class IngredientService : IIngredientService
    {
        private readonly IIngredientRepository _repository;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<IngredientService> _logger;

        public IngredientService(IIngredientRepository repository, AppDbContext dbContext, ILogger<IngredientService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ProductResponse> GetIngredientsAsync(ProductRequest request)
        {
            if (string.IsNullOrEmpty(request.ProductName) && string.IsNullOrEmpty(request.Barcode))
            {
                _logger.LogWarning("Product name or barcode is required.");
                throw new ArgumentException("Product name or barcode is required.");
            }
            if (string.IsNullOrEmpty(request.Country))
            {
                _logger.LogWarning("Country is required.");
                throw new ArgumentException("Country is required.");
            }

            _logger.LogInformation("Fetching ingredients for product: {ProductName}, barcode: {Barcode}, country: {Country}",
                request.ProductName, request.Barcode, request.Country);
            var response = await _repository.GetIngredientsAsync(request);

            if (!response.Ingredients.Any())
            {
                _logger.LogWarning("No ingredients retrieved for product: {ProductName}, country: {Country}", request.ProductName, request.Country);
            }

            response.Ingredients = response.Ingredients.Select(i => new Ingredient
            {
                Id = i.Id,
                Name = i.Name?.Trim(), // Normalize name
                ECode = i.ECode,
                Description = i.Description,
                Status = i.Status,
                IsHalal = i.IsHalal ?? new List<string>(),
                IsHaram = i.IsHaram ?? new List<string>(),
                IsMushbooh = i.IsMushbooh ?? new List<string>(),
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt
            }).ToList();

            await SaveIngredientsToDatabaseAsync(response);
            _logger.LogInformation("Successfully fetched and saved {Count} ingredients for product: {ProductName}", response.Ingredients.Count, request.ProductName);

            return response;
        }

        public async Task<IngredientListResponse> GetIngredientStatusesAsync(IngredientListRequest request)
        {
            if (request == null || request.Ingredients == null || !request.Ingredients.Any())
            {
                _logger.LogWarning("Ingredient list is required.");
                throw new ArgumentException("Ingredient list is required.");
            }

            _logger.LogInformation("Fetching statuses for ingredients: {Ingredients}", string.Join(",", request.Ingredients));
            var response = await _repository.GetIngredientStatusesAsync(request);

            if (!response.Ingredients.Any())
            {
                _logger.LogWarning("No statuses retrieved for ingredients: {Ingredients}", string.Join(",", request.Ingredients));
            }

            response.Ingredients = response.Ingredients.Select(i => new Ingredient
            {
                Id = i.Id,
                Name = i.Name?.Trim(), // Normalize name
                ECode = i.ECode,
                Description = i.Description,
                Status = i.Status,
                IsHalal = i.IsHalal ?? new List<string>(),
                IsHaram = i.IsHaram ?? new List<string>(),
                IsMushbooh = i.IsMushbooh ?? new List<string>(),
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt
            }).ToList();

            await SaveIngredientsToDatabaseAsync(new ProductResponse { Ingredients = response.Ingredients });
            _logger.LogInformation("Successfully fetched and saved statuses for {Count} ingredients", response.Ingredients.Count);

            return response;
        }

        public async Task SaveIngredientsToDatabaseAsync(ProductResponse response)
        {
            if (response?.Ingredients == null || !response.Ingredients.Any())
            {
                _logger.LogWarning("No ingredients to save for country: {Country}", response?.Country);
                return;
            }

            _logger.LogInformation("Saving {Count} ingredients to database for country: {Country}", response.Ingredients.Count, response.Country);
            foreach (var ingredient in response.Ingredients)
            {
                // Validate ingredient name
                if (string.IsNullOrWhiteSpace(ingredient.Name))
                {
                    _logger.LogWarning("Skipping ingredient with empty name");
                    continue;
                }

                // Normalize ingredient name
                var normalizedName = ingredient.Name.Trim();

                // Check for existing ingredient (case-insensitive)
                var existingIngredient = await _dbContext.Ingredients
                    .FirstOrDefaultAsync(i => i.Name.ToLower() == normalizedName.ToLower());

                if (existingIngredient == null)
                {
                    _logger.LogInformation("Creating new ingredient: {IngredientName}", normalizedName);
                    if (ingredient.Id == Guid.Empty)
                    {
                        ingredient.Id = Guid.NewGuid();
                    }
                    ingredient.Name = normalizedName;
                    ingredient.CreatedAt = DateTime.UtcNow;
                    ingredient.UpdatedAt = DateTime.UtcNow;
                    UpdateCountryLists(ingredient, response.Country);
                    _dbContext.Ingredients.Add(ingredient);
                }
                else
                {
                    _logger.LogInformation("Updating existing ingredient: {IngredientName}", normalizedName);
                    // Update existing ingredient fields
                    existingIngredient.Status = ingredient.Status ?? existingIngredient.Status;
                    existingIngredient.ECode = ingredient.ECode ?? existingIngredient.ECode;
                    existingIngredient.Description = ingredient.Description ?? existingIngredient.Description;
                    UpdateCountryLists(existingIngredient, response.Country);
                    existingIngredient.UpdatedAt = DateTime.UtcNow;
                    // Ensure lists are initialized
                    existingIngredient.IsHalal = existingIngredient.IsHalal ?? new List<string>();
                    existingIngredient.IsHaram = existingIngredient.IsHaram ?? new List<string>();
                    existingIngredient.IsMushbooh = existingIngredient.IsMushbooh ?? new List<string>();
                }
            }

            try
            {
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Successfully saved ingredients to database");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error saving ingredients: {Message}", ex.InnerException?.Message);
                throw new Exception("An error occurred while saving the entity changes. See the inner exception for details.", ex);
            }
        }

        private void UpdateCountryLists(Ingredient ingredient, string country)
        {
            // Initialize lists if null
            ingredient.IsHalal = ingredient.IsHalal ?? new List<string>();
            ingredient.IsHaram = ingredient.IsHaram ?? new List<string>();
            ingredient.IsMushbooh = ingredient.IsMushbooh ?? new List<string>();

            // Remove "None" if adding a country
            if (ingredient.IsHalal.Contains("None")) ingredient.IsHalal.Remove("None");
            if (ingredient.IsHaram.Contains("None")) ingredient.IsHaram.Remove("None");
            if (ingredient.IsMushbooh.Contains("None")) ingredient.IsMushbooh.Remove("None");

            // Remove country from other lists to avoid duplicates
            ingredient.IsHalal.Remove(country);
            ingredient.IsHaram.Remove(country);
            ingredient.IsMushbooh.Remove(country);

            // Add country to the appropriate list based on status
            if (ingredient.Status == "Halal")
            {
                if (!ingredient.IsHalal.Contains(country))
                    ingredient.IsHalal.Add(country);
            }
            else if (ingredient.Status == "Haram")
            {
                if (!ingredient.IsHaram.Contains(country))
                    ingredient.IsHaram.Add(country);
            }
            else if (ingredient.Status == "Mushbooh")
            {
                if (!ingredient.IsMushbooh.Contains(country))
                    ingredient.IsMushbooh.Add(country);
            }

            // Re-add "None" if any list is empty
            if (!ingredient.IsHalal.Any()) ingredient.IsHalal.Add("None");
            if (!ingredient.IsHaram.Any()) ingredient.IsHaram.Add("None");
            if (!ingredient.IsMushbooh.Any()) ingredient.IsMushbooh.Add("None");
        }

        public async Task<ProductEvaluationResponse> EvaluateProductStatusAsync(Guid productId)
        {
            _logger.LogInformation("Evaluating product status for product ID: {ProductId}", productId);
            var product = await _dbContext.Products
                .Include(p => p.ProductIngredients)
                .ThenInclude(pi => pi.Ingredient)
                .FirstOrDefaultAsync(p => p.ID == productId);

            if (product == null)
            {
                _logger.LogWarning("Product with ID {ProductId} not found.", productId);
                throw new ArgumentException($"Product with ID {productId} not found.");
            }

            var ingredients = product.ProductIngredients
                .Select(pi => pi.Ingredient)
                .ToList();

            string status = EvaluateIngredientStatuses(ingredients);

            product.Status = status;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Product status evaluated as {Status} for product ID: {ProductId}", status, productId);
            return new ProductEvaluationResponse
            {
                ProductId = product.ID,
                ProductName = product.ProductName,
                Status = status,
                Country = product.Country,
                Ingredients = ingredients
            };
        }

        private string EvaluateIngredientStatuses(List<Ingredient> ingredients)
        {
            if (!ingredients.Any())
            {
                return "Unknown";
            }
            if (ingredients.Any(i => i.Status == "Haram"))
            {
                return "Haram";
            }
            else if (ingredients.Any(i => i.Status == "Mushbooh"))
            {
                return "Mushbooh";
            }
            else if (ingredients.All(i => i.Status == "Halal"))
            {
                return "Halal";
            }
            else
            {
                return "Unknown";
            }
        }
    }
}