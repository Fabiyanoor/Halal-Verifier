using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HalalProject.Database.Data;
using HalalProject.Model.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Halal_project_BL.Repositories;

public interface IProductRepository
{
    Task<List<Product>> GetProducts();
    Task<Product?> GetProduct(Guid id);
    Task<Product> CreateProduct(Product product);
    Task UpdateProduct(Product product);
    Task DeleteProduct(Guid id);
    Task<bool> ProductExists(Guid id);
    Task<List<Product>> GetProductsFiltered(string? name, List<string> categories, string? country, string? status);
    Task<List<string>> GetCategories();
    Task<List<string>> GetCountries();
    Task<List<Product>> GetProductsByName(string name);
    Task<Ingredient> CreateIngredient(Ingredient ingredient);
    Task<ProductIngredient> CreateProductIngredient(ProductIngredient productIngredient);
    Task RemoveProductIngredients(Guid productId);
}

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProductRepository> _logger;

    public ProductRepository(AppDbContext context, ILogger<ProductRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Product> CreateProduct(Product product)
    {
        _logger.LogInformation("Creating product: {ProductName}", product.ProductName);
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully created product with ID {Id}", product.ID);
        return product;
    }

    public async Task DeleteProduct(Guid id)
    {
        _logger.LogInformation("Deleting product with ID {Id}", id);
        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            await RemoveProductIngredients(id);
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully deleted product with ID {Id}", id);
        }
        else
        {
            _logger.LogWarning("Product with ID {Id} not found for deletion", id);
        }
    }

    public async Task<Product?> GetProduct(Guid id)
    {
        _logger.LogInformation("Fetching product with ID {Id}", id);
        var product = await _context.Products
            .Include(p => p.ProductIngredients)
            .ThenInclude(pi => pi.Ingredient)
            .FirstOrDefaultAsync(p => p.ID == id);
        _logger.LogInformation(product != null ? "Successfully fetched product with ID {Id}" : "Product with ID {Id} not found", id);
        return product;
    }
    public async Task<Ingredient> CreateIngredient(Ingredient ingredient)
    {
        var existing = await _context.Ingredients
            .FirstOrDefaultAsync(i => i.Name.ToLower() == ingredient.Name.ToLower());

        if (existing != null)
        {
            return existing;
        }

        await _context.Ingredients.AddAsync(ingredient);
        await _context.SaveChangesAsync();
        return ingredient;
    }

    public async Task<ProductIngredient> CreateProductIngredient(ProductIngredient productIngredient)
    {
        _logger.LogInformation("Creating product ingredient link for ProductId: {ProductId}, IngredientId: {IngredientId}",
            productIngredient.ProductId, productIngredient.IngredientId);
        await _context.ProductIngredients.AddAsync(productIngredient);
        await _context.SaveChangesAsync();
        return productIngredient;
    }
    public async Task<List<Product>> GetProducts()
    {
        _logger.LogInformation("Fetching all products");
        try
        {
            var products = await _context.Products
                .Include(p => p.ProductIngredients)
                .ThenInclude(pi => pi.Ingredient)
                .ToListAsync();
            _logger.LogInformation("Successfully fetched {Count} products", products.Count);
            return products;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching products: {Message}", ex.Message);
            throw;
        }
    }

    public async Task UpdateProduct(Product product)
    {
        _logger.LogInformation("Updating product with ID {Id}", product.ID);
        _context.Entry(product).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully updated product with ID {Id}", product.ID);
    }

    public async Task<bool> ProductExists(Guid id)
    {
        _logger.LogInformation("Checking if product with ID {Id} exists", id);
        var exists = await _context.Products.AnyAsync(p => p.ID == id);
        _logger.LogInformation("Product with ID {Id} {Exists}", id, exists ? "exists" : "does not exist");
        return exists;
    }

    public async Task<List<string>> GetCategories()
    {
        _logger.LogInformation("Fetching all categories");
        var categories = await _context.Products
            .Where(p => p.Category != null)
            .Select(p => p.Category)
            .Distinct()
            .ToListAsync();
        _logger.LogInformation("Successfully fetched {Count} categories", categories.Count);
        return categories;
    }

    public async Task<List<string>> GetCountries()
    {
        _logger.LogInformation("Fetching all countries");
        var countries = await _context.Products
            .Where(p => p.Country != null)
            .Select(p => p.Country)
            .Distinct()
            .ToListAsync();
        _logger.LogInformation("Successfully fetched {Count} countries", countries.Count);
        return countries;
    }

    public async Task<List<Product>> GetProductsByName(string name)
    {
        _logger.LogInformation("Searching products by name: {Name}", name);
        var products = await _context.Products
            .Where(p => EF.Functions.Like(p.ProductName, $"%{name}%"))
            .Include(p => p.ProductIngredients)
            .ThenInclude(pi => pi.Ingredient)
            .ToListAsync();
        _logger.LogInformation("Found {Count} products for name: {Name}", products.Count, name);
        return products;
    }

    public async Task<List<Product>> GetProductsFiltered(string? name, List<string> categories, string? country, string? status)
    {
        _logger.LogInformation("Filtering products with name: {Name}, categories: {Categories}, country: {Country}, status: {Status}",
            name, string.Join(",", categories), country, status);
        var query = _context.Products
            .Include(p => p.ProductIngredients)
            .ThenInclude(pi => pi.Ingredient)
            .AsQueryable();

        if (!string.IsNullOrEmpty(name))
        {
            query = query.Where(p => EF.Functions.Like(p.ProductName, $"%{name}%"));
        }

        if (categories != null && categories.Any())
        {
            query = query.Where(p => p.Category != null && categories.Contains(p.Category));
        }

        if (!string.IsNullOrEmpty(country))
        {
            query = query.Where(p => p.Country == country);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(p => p.Status == status);
        }

        var products = await query.ToListAsync();
        _logger.LogInformation("Found {Count} products matching filter criteria", products.Count);
        return products;
    }

    public async Task RemoveProductIngredients(Guid productId)
    {
        _logger.LogInformation("Removing product ingredients for product ID {ProductId}", productId);
        var productIngredients = await _context.ProductIngredients
            .Where(pi => pi.ProductId == productId)
            .ToListAsync();
        _context.ProductIngredients.RemoveRange(productIngredients);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully removed {Count} product ingredients for product ID {ProductId}", productIngredients.Count, productId);
    }
}