using Microsoft.AspNetCore.Mvc;
using HalalProject.Model.Models;
using Microsoft.Extensions.Logging;
using HalalProject.Model.Entites;
using Halal_project_BL.Services;
using HalalProject.Model.DTO;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;

namespace Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly ILogger<ProductController> _logger;
        private readonly IWebHostEnvironment _env;

        public ProductController(
            IProductService productService,
            ILogger<ProductController> logger,
            IWebHostEnvironment env)
        {
            _productService = productService;
            _logger = logger;
            _env = env;
        }

        [HttpGet]
        [ProducesResponseType(typeof(BaseResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BaseResponseModel>> GetProducts()
        {
            try
            {
                _logger.LogInformation("Fetching all products");
                var products = await _productService.GetProducts();
                _logger.LogInformation("Successfully fetched {Count} products", products.Count);

                // Return only the needed fields
                var responseData = products.Select(p => new
                {
                    ID = p.ID,
                    ProductName = p.ProductName,
                    Country = p.Country,
                    Status = p.Status,
                    ImageUrl = p.ImageUrl,
                    Category = p.Category // Included for filtering
                }).ToList();

                return Ok(new BaseResponseModel
                {
                    Success = true,
                    Data = responseData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching products: {Message}", ex.Message);
                return StatusCode(500, new BaseResponseModel
                {
                    Success = false,
                    ErrorMessage = "Error fetching products: " + ex.Message
                });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BaseResponseModel>> GetProduct(Guid id)
        {
            try
            {
                _logger.LogInformation("Fetching product with ID {Id}", id);
                var product = await _productService.GetProduct(id);
                if (product == null)
                {
                    _logger.LogWarning("Product with ID {Id} not found", id);
                    return NotFound(new BaseResponseModel
                    {
                        Success = false,
                        ErrorMessage = "Product not found"
                    });
                }
                _logger.LogInformation("Successfully fetched product with ID {Id}", id);
                return Ok(new BaseResponseModel { Success = true, Data = product });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product with ID {Id}: {Message}", id, ex.Message);
                return StatusCode(500, new BaseResponseModel
                {
                    Success = false,
                    ErrorMessage = "Error getting product: " + ex.Message
                });
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(BaseResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BaseResponseModel>> CreateProduct([FromForm] ProductCreateDto productDto)
        {
            try
            {
                _logger.LogInformation("Creating product with ProductName: {ProductName}, Country: {Country}, Barcode: {Barcode}, UseAI: {UseAI}, IngredientNames: {IngredientNames}",
                    productDto.ProductName, productDto.Country, productDto.Barcode, productDto.UseAI, string.Join(",", productDto.IngredientNames ?? new List<string>()));

                // Validate required fields
                if (string.IsNullOrEmpty(productDto.ProductName) || string.IsNullOrEmpty(productDto.Country) || string.IsNullOrEmpty(productDto.Description))
                {
                    _logger.LogWarning("Validation failed: Product name, country, and description are required.");
                    return BadRequest(new BaseResponseModel
                    {
                        Success = false,
                        ErrorMessage = "Product name, country, and description are required."
                    });
                }
                if (productDto.UseAI && string.IsNullOrEmpty(productDto.Barcode))
                {
                    _logger.LogWarning("Validation failed: Barcode is required when UseAI is true.");
                    return BadRequest(new BaseResponseModel
                    {
                        Success = false,
                        ErrorMessage = "Barcode is required when UseAI is true."
                    });
                }

                // Validate image type and size
                if (productDto.Image != null && productDto.Image.Length > 0)
                {
                    var validExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    var extension = Path.GetExtension(productDto.Image.FileName).ToLower();
                    if (!validExtensions.Contains(extension))
                    {
                        _logger.LogWarning("Invalid image extension: {Extension}", extension);
                        return BadRequest(new BaseResponseModel
                        {
                            Success = false,
                            ErrorMessage = "Only JPG and PNG images are allowed."
                        });
                    }
                    if (productDto.Image.Length > 5 * 1024 * 1024) // 5MB limit
                    {
                        _logger.LogWarning("Image size exceeds 5MB limit.");
                        return BadRequest(new BaseResponseModel
                        {
                            Success = false,
                            ErrorMessage = "Image size must be less than 5MB."
                        });
                    }
                }

                string imageUrl = null;
                if (productDto.Image != null && productDto.Image.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_env.WebRootPath, "Assets");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(productDto.Image.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await productDto.Image.CopyToAsync(fileStream);
                    }

                    imageUrl = $"/Assets/{uniqueFileName}";
                    _logger.LogInformation("Image uploaded: {ImageUrl}", imageUrl);
                }
                else
                {
                    _logger.LogInformation("No image provided for product");
                }

                var product = new Product
                {
                    ProductName = productDto.ProductName,
                    Country = productDto.Country,
                    Description = productDto.Description,
                    Barcode = productDto.Barcode,
                    Category = productDto.Category,
                    ImageUrl = imageUrl,
                    AddedBy = "Admin",
                    CreatedAt = DateTime.UtcNow
                };

                var createdProduct = await _productService.CreateProduct(
                    product,
                    productDto.UseAI ? null : productDto.IngredientNames,
                    productDto.Barcode,
                    productDto.UseAI);
                _logger.LogInformation("Successfully created product with ID {Id}, Status: {Status}", createdProduct.ID, createdProduct.Status);

                return Ok(new BaseResponseModel
                {
                    Success = true,
                    Data = new { Id = createdProduct.ID, Status = createdProduct.Status }
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Validation error creating product: {Message}", ex.Message);
                return BadRequest(new BaseResponseModel
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product: {Message}, StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
                return StatusCode(500, new BaseResponseModel
                {
                    Success = false,
                    ErrorMessage = "Error creating product: " + ex.Message
                });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<BaseResponseModel>> UpdateProduct(Guid id, [FromForm] ProductUpdateDto productDto)
        {
            try
            {
                // Validate image type and size
                if (productDto.Image != null && productDto.Image.Length > 0)
                {
                    var validExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    var extension = Path.GetExtension(productDto.Image.FileName).ToLower();
                    if (!validExtensions.Contains(extension))
                    {
                        return BadRequest(new BaseResponseModel
                        {
                            Success = false,
                            ErrorMessage = "Only JPG and PNG images are allowed."
                        });
                    }
                    if (productDto.Image.Length > 5 * 1024 * 1024) // 5MB limit
                    {
                        return BadRequest(new BaseResponseModel
                        {
                            Success = false,
                            ErrorMessage = "Image size must be less than 5MB."
                        });
                    }
                }

                _logger.LogInformation("Updating product with ID {Id}", id);
                var existingProduct = await _productService.GetProduct(id);
                if (existingProduct == null)
                {
                    _logger.LogWarning("Product with ID {Id} not found", id);
                    return NotFound(new BaseResponseModel
                    {
                        Success = false,
                        ErrorMessage = "Product not found"
                    });
                }

                // Handle image update
                if (productDto.Image != null && productDto.Image.Length > 0)
                {
                    // Delete old image if exists
                    if (!string.IsNullOrEmpty(existingProduct.ImageUrl))
                    {
                        var oldImagePath = Path.Combine(_env.WebRootPath, existingProduct.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    // Save new image
                    var uploadsFolder = Path.Combine(_env.WebRootPath, "Assets");
                    var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(productDto.Image.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await productDto.Image.CopyToAsync(fileStream);
                    }

                    existingProduct.ImageUrl = $"/Assets/{uniqueFileName}";
                }

                // Prepare updated product
                var updatedProduct = new Product
                {
                    ID = id,
                    ProductName = productDto.ProductName,
                    Country = productDto.Country,
                    Description = productDto.Description,
                    Barcode = productDto.Barcode,
                    Category = productDto.Category,
                    ImageUrl = existingProduct.ImageUrl,
                    AddedBy = existingProduct.AddedBy,
                    CreatedAt = existingProduct.CreatedAt
                };

                await _productService.UpdateProduct(
                    id,
                    updatedProduct,
                    productDto.IngredientNames,
                    productDto.Barcode,
                    productDto.UseAI);

                _logger.LogInformation("Successfully updated product with ID {Id}", id);
                return Ok(new BaseResponseModel { Success = true });
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Validation error updating product with ID {Id}: {Message}", id, ex.Message);
                return BadRequest(new BaseResponseModel
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product with ID {Id}: {Message}", id, ex.Message);
                return StatusCode(500, new BaseResponseModel
                {
                    Success = false,
                    ErrorMessage = "Error updating product: " + ex.Message
                });
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<BaseResponseModel>> DeleteProduct(Guid id)
        {
            try
            {
                _logger.LogInformation("Deleting product with ID {Id}", id);
                var product = await _productService.GetProduct(id);
                if (product == null)
                {
                    _logger.LogWarning("Product with ID {Id} not found", id);
                    return NotFound(new BaseResponseModel
                    {
                        Success = false,
                        ErrorMessage = "Product not found"
                    });
                }

                // Delete associated image file
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    var imagePath = Path.Combine(_env.WebRootPath, product.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                await _productService.DeleteProduct(id);
                _logger.LogInformation("Successfully deleted product with ID {Id}", id);

                return Ok(new BaseResponseModel { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product with ID {Id}: {Message}", id, ex.Message);
                return StatusCode(500, new BaseResponseModel
                {
                    Success = false,
                    ErrorMessage = "Error deleting product: " + ex.Message
                });
            }
        }

        [HttpGet("search")]
        public async Task<ActionResult<BaseResponseModel>> GetProductsFiltered(
            [FromQuery] string name,
            [FromQuery] string categories,
            [FromQuery] string country,
            [FromQuery] string status)
        {
            try
            {
                _logger.LogInformation("Filtering products with name: {Name}, categories: {Categories}, country: {Country}, status: {Status}",
                    name, categories, country, status);
                var categoryList = !string.IsNullOrEmpty(categories)
                    ? categories.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    : new List<string>();

                var products = await _productService.GetProductsFiltered(name, categoryList, country, status);
                _logger.LogInformation("Found {Count} products matching filter criteria", products.Count);
                return Ok(new BaseResponseModel { Success = true, Data = products });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering products: {Message}", ex.Message);
                return StatusCode(500, new BaseResponseModel
                {
                    Success = false,
                    ErrorMessage = "Error filtering products: " + ex.Message
                });
            }
        }

        [HttpGet("categories")]
        public async Task<ActionResult<BaseResponseModel>> GetCategories()
        {
            try
            {
                _logger.LogInformation("Fetching all categories");
                var categories = await _productService.GetCategories();
                _logger.LogInformation("Successfully fetched {Count} categories", categories.Count);
                return Ok(new BaseResponseModel { Success = true, Data = categories });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories: {Message}", ex.Message);
                return StatusCode(500, new BaseResponseModel
                {
                    Success = false,
                    ErrorMessage = "Error getting categories: " + ex.Message
                });
            }
        }


        [HttpPost("with-ingredients")]
        public async Task<ActionResult<BaseResponseModel>> CreateProductWithImageUrl(
           [FromBody] ProductWithImageUrlDto productDto)
        {
            try
            {
                // Validate model
                if (!ModelState.IsValid)
                {
                    return BadRequest(new BaseResponseModel
                    {
                        Success = false,
                        ErrorMessage = string.Join("; ", ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage))
                    });
                }

                // Validate image URL
                if (string.IsNullOrWhiteSpace(productDto.ImageUrl))
                {
                    return BadRequest(new BaseResponseModel
                    {
                        Success = false,
                        ErrorMessage = "Image URL is required"
                    });
                }

                // Create product with the provided image URL
                   var createdProduct = await _productService.CreateProductWithImageUrl(
        productDto.ProductName,
        productDto.Country,
        productDto.Description,
        productDto.Category,
        productDto.Barcode,
        productDto.ImageUrl,
        productDto.Status, // Pass the status
        productDto.Ingredients);

                return Ok(new BaseResponseModel
                {
                    Success = true,
                    Data = createdProduct
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product with image URL");
                return StatusCode(500, new BaseResponseModel
                {
                    Success = false,
                    ErrorMessage = "Error creating product"
                });
            }
        }

        private async Task<string> UploadImage(IFormFile imageFile)
        {
            // Validate image
            var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(imageFile.FileName).ToLower();
            if (!validExtensions.Contains(extension))
            {
                throw new ArgumentException("Only JPG, PNG, and GIF images are allowed.");
            }

            if (imageFile.Length > 5 * 1024 * 1024) // 5MB
            {
                throw new ArgumentException("Image size must be less than 5MB.");
            }

            // Create uploads directory if it doesn't exist
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "products");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Generate unique filename
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // Save the file
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }

            return $"/uploads/products/{uniqueFileName}";
        }
        [HttpGet("countries")]
        public async Task<ActionResult<BaseResponseModel>> GetCountries()
        {
            try
            {
                _logger.LogInformation("Fetching all countries");
                var countries = await _productService.GetCountries();
                _logger.LogInformation("Successfully fetched {Count} countries", countries.Count);
                return Ok(new BaseResponseModel { Success = true, Data = countries });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting countries: {Message}", ex.Message);
                return StatusCode(500, new BaseResponseModel
                {
                    Success = false,
                    ErrorMessage = "Error getting countries: " + ex.Message
                });
            }
        }
    }
}