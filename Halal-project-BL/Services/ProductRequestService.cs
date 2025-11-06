using Halal_project_BL.Repositories;
using HalalProject.Database.Data;
using HalalProject.Model.DTO;
using HalalProject.Model.Entites;
using HalalProject.Model.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Halal_project_BL.Services
{
    public interface IProductChangeRequestService
    {
        Task<ProductRequestResponse> CreateProductRequest(ProductChangeRequest request, string username);
        Task<ProductRequestResponse> UpdateProductRequest(UpdateProductRequestDto request, string username);
        Task<ProductRequestResponse> DeleteProductRequest(Guid productId, string username);
        Task<ProcessRequestResponse> ApproveRequest(Guid requestId, string adminUsername, List<Ingredient> finalIngredients);
        Task<ProcessRequestResponse> RejectRequest(Guid requestId, string adminUsername, string rejectionReason);
        Task<List<ProductChangeRequest>> GetAllRequests();
            Task<List<ProductChangeRequest>> GetUserRequests(string username);
        Task<ProductRequestResponse> EditUserRequest(EditProductRequestDto request, string username);
        Task<ProductChangeRequest?> GetRequestDetails(Guid requestId);
        Task<List<Ingredient>> VerifyIngredients(Guid requestId);
    }

    public class ProductChangeRequestService : IProductChangeRequestService
    {
        private readonly AppDbContext _dbContext;
        private readonly IProductService _productService;
        private readonly IDistributedCache _cacheService;
        private readonly IIngredientService _ingredientService;
        private readonly ILogger<ProductChangeRequestService> _logger;
        private readonly IProductRequestRepository _requestRepository;

        public ProductChangeRequestService(
            AppDbContext dbContext,
            IProductService productService,
            IDistributedCache cacheService,
            IIngredientService ingredientService,
            ILogger<ProductChangeRequestService> logger,
            IProductRequestRepository requestRepository)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _ingredientService = ingredientService ?? throw new ArgumentNullException(nameof(ingredientService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        }

        public async Task<ProductRequestResponse> CreateProductRequest(ProductChangeRequest request, string username)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrEmpty(request.ProductName) || string.IsNullOrEmpty(request.Country))
            {
                _logger.LogWarning("Product name and country are required for create request by user {Username}", username);
                return new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = "Product name and country are required"
                };
            }

            try
            {
                request.RequestId = Guid.NewGuid();
                request.RequestType = "Add";
                request.RequestStatus = "Pending";
                request.RequestedBy = username;
                request.RequestDate = DateTime.UtcNow;

                await _dbContext.ProductChangeRequests.AddAsync(request);
                await _dbContext.SaveChangesAsync();

                await _cacheService.RemoveAsync("pending_requests");

                _logger.LogInformation("Product creation request submitted successfully: RequestId={RequestId}", request.RequestId);
                return new ProductRequestResponse
                {
                    RequestId = request.RequestId,
                    IsSuccess = true,
                    Message = "Product creation request submitted successfully",
                    Request = request
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product request for ProductName: {ProductName}", request.ProductName);
                return new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = $"Error creating request: {ex.Message}"
                };
            }
        }

        public async Task<ProductRequestResponse> UpdateProductRequest(UpdateProductRequestDto requestDto, string username)
        {
            if (requestDto == null || requestDto.ProductId == Guid.Empty)
            {
                _logger.LogWarning("Invalid update request: ProductId={ProductId}", requestDto?.ProductId);
                return new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = "Product ID is required"
                };
            }

            if (string.IsNullOrEmpty(requestDto.Country))
            {
                _logger.LogWarning("Country is required for update request: ProductId={ProductId}", requestDto.ProductId);
                return new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = "Country is required"
                };
            }

            bool hasUpdate = !string.IsNullOrEmpty(requestDto.ProductName) ||
                            !string.IsNullOrEmpty(requestDto.Barcode) ||
                            !string.IsNullOrEmpty(requestDto.Category) ||
                            !string.IsNullOrEmpty(requestDto.Description) ||
                            !string.IsNullOrEmpty(requestDto.ImageUrl) ||
                            (requestDto.Ingredients != null && requestDto.Ingredients.Any());
            if (!hasUpdate && !requestDto.UseOnlyUserIngredients)
            {
                _logger.LogWarning("No fields provided for update: ProductId={ProductId}", requestDto.ProductId);
                return new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = "At least one field must be provided for update or use only user ingredients must be specified"
                };
            }

            try
            {
                var product = await _dbContext.Products
                    .Include(p => p.ProductIngredients)
                    .ThenInclude(pi => pi.Ingredient)
                    .FirstOrDefaultAsync(p => p.ID == requestDto.ProductId);
                if (product == null)
                {
                    _logger.LogWarning("Product not found for update request: ProductId={ProductId}", requestDto.ProductId);
                    return new ProductRequestResponse
                    {
                        IsSuccess = false,
                        Message = "Product not found"
                    };
                }

                if (product.AddedBy != username)
                {
                    _logger.LogWarning("User {Username} attempted to update product {ProductId} owned by {Owner}",
                        username, requestDto.ProductId, product.AddedBy);
                    return new ProductRequestResponse
                    {
                        IsSuccess = false,
                        Message = "You can only update your own products"
                    };
                }

                var request = new ProductChangeRequest
                {
                    RequestId = Guid.NewGuid(),
                    RequestType = "Edit",
                    RequestStatus = "Pending",
                    RequestedBy = username,
                    RequestDate = DateTime.UtcNow,
                    ProductId = requestDto.ProductId,
                    ProductName = requestDto.ProductName ?? product.ProductName,
                    Country = requestDto.Country,
                    Category = requestDto.Category ?? product.Category,
                    Description = requestDto.Description ?? product.Description,
                    ImageUrl = requestDto.ImageUrl ?? product.ImageUrl,
                    Barcode = requestDto.Barcode ?? product.Barcode,
                    Ingredients = requestDto.Ingredients ?? product.ProductIngredients?.Select(pi => pi.Ingredient.Name).ToList(),
                    UseOnlyUserIngredients = requestDto.UseOnlyUserIngredients
                };

                await _dbContext.ProductChangeRequests.AddAsync(request);
                await _dbContext.SaveChangesAsync();

                await _cacheService.RemoveAsync("pending_requests");

                _logger.LogInformation("Product update request submitted successfully: RequestId={RequestId}", request.RequestId);
                return new ProductRequestResponse
                {
                    RequestId = request.RequestId,
                    IsSuccess = true,
                    Message = "Product update request submitted successfully",
                    Request = request
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating update request: ProductId={ProductId}", requestDto.ProductId);
                return new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = $"Error creating update request: {ex.Message}"
                };
            }
        }

        public async Task<ProductRequestResponse> EditUserRequest(EditProductRequestDto requestDto, string username)
        {
            if (requestDto == null || requestDto.RequestId == Guid.Empty)
            {
                _logger.LogWarning("Invalid edit request: RequestId={RequestId}", requestDto?.RequestId);
                return new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = "Request ID is required"
                };
            }

            if (string.IsNullOrEmpty(requestDto.Country))
            {
                _logger.LogWarning("Country is required for edit request: RequestId={RequestId}", requestDto.RequestId);
                return new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = "Country is required"
                };
            }

            try
            {
                var existingRequest = await _dbContext.ProductChangeRequests
                    .FirstOrDefaultAsync(r => r.RequestId == requestDto.RequestId);
                if (existingRequest == null)
                {
                    _logger.LogWarning("Request not found for edit: RequestId={RequestId}", requestDto.RequestId);
                    return new ProductRequestResponse
                    {
                        IsSuccess = false,
                        Message = "Request not found"
                    };
                }

                if (existingRequest.RequestedBy != username)
                {
                    _logger.LogWarning("User {Username} attempted to edit request {RequestId} owned by {Owner}",
                        username, requestDto.RequestId, existingRequest.RequestedBy);
                    return new ProductRequestResponse
                    {
                        IsSuccess = false,
                        Message = "You can only edit your own requests"
                    };
                }

                if (existingRequest.RequestStatus != "Pending")
                {
                    _logger.LogWarning("Request already processed: RequestId={RequestId}, Status={Status}",
                        requestDto.RequestId, existingRequest.RequestStatus);
                    return new ProductRequestResponse
                    {
                        IsSuccess = false,
                        Message = "Request is already processed"
                    };
                }

                existingRequest.ProductName = requestDto.ProductName ?? existingRequest.ProductName;
                existingRequest.Country = requestDto.Country;
                existingRequest.Category = requestDto.Category ?? existingRequest.Category;
                existingRequest.Description = requestDto.Description ?? existingRequest.Description;
                existingRequest.ImageUrl = requestDto.ImageUrl ?? existingRequest.ImageUrl;
                existingRequest.Barcode = requestDto.Barcode ?? existingRequest.Barcode;
                existingRequest.Ingredients = requestDto.Ingredients ?? existingRequest.Ingredients;
                existingRequest.UseOnlyUserIngredients = requestDto.UseOnlyUserIngredients;

                await _dbContext.SaveChangesAsync();
                await _cacheService.RemoveAsync("pending_requests");

                _logger.LogInformation("Product request edited successfully: RequestId={RequestId}", requestDto.RequestId);
                return new ProductRequestResponse
                {
                    RequestId = requestDto.RequestId,
                    IsSuccess = true,
                    Message = "Product request edited successfully",
                    Request = existingRequest
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing request: RequestId={RequestId}", requestDto.RequestId);
                return new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = $"Error editing request: {ex.Message}"
                };
            }
        }

        public async Task<ProductRequestResponse> DeleteProductRequest(Guid productId, string username)
        {
            try
            {
                var existingProduct = await _productService.GetProduct(productId);
                if (existingProduct == null)
                {
                    _logger.LogWarning("Product not found for delete request: ProductId={ProductId}", productId);
                    return new ProductRequestResponse
                    {
                        IsSuccess = false,
                        Message = "Product not found"
                    };
                }

                if (existingProduct.AddedBy != username)
                {
                    _logger.LogWarning("User {Username} attempted to delete product {ProductId} owned by {Owner}",
                        username, productId, existingProduct.AddedBy);
                    return new ProductRequestResponse
                    {
                        IsSuccess = false,
                        Message = "You can only delete your own products"
                    };
                }

                var request = new ProductChangeRequest
                {
                    RequestId = Guid.NewGuid(),
                    RequestType = "Delete",
                    RequestStatus = "Pending",
                    RequestedBy = username,
                    RequestDate = DateTime.UtcNow,
                    ProductId = productId,
                    ProductName = existingProduct.ProductName,
                    Country = existingProduct.Country,
                    OriginalProduct = existingProduct
                };

                await _dbContext.ProductChangeRequests.AddAsync(request);
                await _dbContext.SaveChangesAsync();

                await _cacheService.RemoveAsync("pending_requests");

                _logger.LogInformation("Product deletion request submitted successfully: RequestId={RequestId}", request.RequestId);
                return new ProductRequestResponse
                {
                    RequestId = request.RequestId,
                    IsSuccess = true,
                    Message = "Product deletion request submitted successfully",
                    Request = request
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating delete request for product: ProductId={ProductId}", productId);
                return new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = $"Error deleting request: {ex.Message}"
                };
            }
        }

        public async Task<List<Ingredient>> VerifyIngredients(Guid requestId)
        {
            try
            {
                var request = await _dbContext.ProductChangeRequests
                    .FirstOrDefaultAsync(r => r.RequestId == requestId);

                if (request == null)
                {
                    _logger.LogWarning("Request not found: RequestId={RequestId}", requestId);
                    return new List<Ingredient>();
                }

                if (request.RequestStatus != "Pending")
                {
                    _logger.LogWarning("Request already processed: RequestId={RequestId}, Status={Status}", requestId, request.RequestStatus);
                    return new List<Ingredient>();
                }

                List<Ingredient> verifiedIngredients = new List<Ingredient>();

                if (request.UseOnlyUserIngredients && request.Ingredients != null && request.Ingredients.Any())
                {
                    _logger.LogInformation("Verifying user-provided ingredients for RequestId={RequestId}", requestId);
                    var ingredientResponse = await _ingredientService.GetIngredientStatusesAsync(new IngredientListRequest
                    {
                        Ingredients = request.Ingredients
                    });

                    verifiedIngredients = ingredientResponse.Ingredients;
                    await _ingredientService.SaveIngredientsToDatabaseAsync(new ProductResponse { Ingredients = verifiedIngredients });
                }
                else
                {
                    _logger.LogInformation("Fetching AI ingredients for ProductName={ProductName}, Country={Country}, RequestId={RequestId}",
                        request.ProductName, request.Country, requestId);
                    var aiIngredientResponse = await _ingredientService.GetIngredientsAsync(new ProductRequest
                    {
                        ProductName = request.ProductName,
                        Country = request.Country,
                        Barcode = request.Barcode
                    });

                    verifiedIngredients = aiIngredientResponse.Ingredients;
                    await _ingredientService.SaveIngredientsToDatabaseAsync(aiIngredientResponse);

                    if (request.Ingredients != null && request.Ingredients.Any())
                    {
                        _logger.LogInformation("Verifying user-provided ingredients for RequestId={RequestId}", requestId);
                        var userIngredientResponse = await _ingredientService.GetIngredientStatusesAsync(new IngredientListRequest
                        {
                            Ingredients = request.Ingredients
                        });
                        await _ingredientService.SaveIngredientsToDatabaseAsync(new ProductResponse { Ingredients = userIngredientResponse.Ingredients });
                    }
                }

                _logger.LogInformation("Ingredients verified for RequestId={RequestId}: {VerifiedCount} ingredients", requestId, verifiedIngredients.Count);
                return verifiedIngredients;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying ingredients for RequestId={RequestId}", requestId);
                return new List<Ingredient>();
            }
        }

        public async Task<ProcessRequestResponse> ApproveRequest(Guid requestId, string adminUsername, List<Ingredient> finalIngredients)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("Approving request: RequestId={RequestId}, Admin={AdminUsername}, FinalIngredients={IngredientCount}",
                    requestId, adminUsername, finalIngredients?.Count ?? 0);

                var request = await _dbContext.ProductChangeRequests
                    .Include(r => r.OriginalProduct)
                    .FirstOrDefaultAsync(r => r.RequestId == requestId);

                if (request == null)
                {
                    _logger.LogWarning("Request not found: RequestId={RequestId}", requestId);
                    return new ProcessRequestResponse
                    {
                        RequestId = requestId,
                        IsSuccess = false,
                        Message = "Request not found"
                    };
                }

                if (request.RequestStatus != "Pending")
                {
                    _logger.LogWarning("Request already processed: RequestId={RequestId}, Status={Status}", requestId, request.RequestStatus);
                    return new ProcessRequestResponse
                    {
                        RequestId = requestId,
                        IsSuccess = false,
                        Message = "Request already processed"
                    };
                }

                Product? product = null;

                switch (request.RequestType)
                {
                    case "Add":
                        try
                        {
                            product = new Product
                            {
                                ID = Guid.NewGuid(),
                                ProductName = request.ProductName ?? $"Product_{Guid.NewGuid().ToString().Substring(0, 8)}",
                                Country = request.Country,
                                Category = request.Category ?? "Unknown",
                                Description = request.Description ?? "No description provided",
                                ImageUrl = request.ImageUrl,
                                Barcode = request.Barcode,
                                AddedBy = request.RequestedBy,
                                VerifiedBy = adminUsername,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                Status = "Pending",
                                ProductIngredients = new List<ProductIngredient>()
                            };

                            _dbContext.Products.Add(product);
                            await _dbContext.SaveChangesAsync();

                            if (finalIngredients != null && finalIngredients.Any())
                            {
                                foreach (var ingredient in finalIngredients)
                                {
                                    var dbIngredient = await _dbContext.Ingredients
                                        .FirstOrDefaultAsync(i => i.Id == ingredient.Id || i.Name.ToLower() == ingredient.Name.ToLower());
                                    if (dbIngredient == null)
                                    {
                                        dbIngredient = new Ingredient
                                        {
                                            Id = ingredient.Id != Guid.Empty ? ingredient.Id : Guid.NewGuid(),
                                            Name = ingredient.Name,
                                            Status = ingredient.Status ?? "Unknown",
                                            ECode = ingredient.ECode ?? "N/A",
                                            Description = ingredient.Description ?? ingredient.Name,
                                            IsHalal = ingredient.IsHalal ?? new List<string> { "None" },
                                            IsHaram = ingredient.IsHaram ?? new List<string> { "None" },
                                            IsMushbooh = ingredient.IsMushbooh ?? new List<string> { "None" },
                                            CreatedAt = ingredient.CreatedAt,
                                            UpdatedAt = ingredient.UpdatedAt ?? DateTime.UtcNow,
                                            ProductIngredients = new List<ProductIngredient>()
                                        };
                                        _dbContext.Ingredients.Add(dbIngredient);
                                        await _dbContext.SaveChangesAsync();
                                    }

                                    var productIngredient = new ProductIngredient
                                    {
                                        ProductId = product.ID,
                                        IngredientId = dbIngredient.Id,
                                        CreatedAt = DateTime.UtcNow
                                    };
                                    _dbContext.ProductIngredients.Add(productIngredient);
                                }
                                await _dbContext.SaveChangesAsync();
                            }

                            // Update product status
                            var evaluationResponse = await _ingredientService.EvaluateProductStatusAsync(product.ID);
                            product.Status = evaluationResponse.Status;
                            _dbContext.Products.Update(product);
                            await _dbContext.SaveChangesAsync();
                        }
                        catch (DbUpdateException ex)
                        {
                            _logger.LogError(ex, "Database error adding product for RequestId={RequestId}: {Error}",
                                requestId, ex.InnerException?.Message ?? ex.Message);
                            await transaction.RollbackAsync();
                            return new ProcessRequestResponse
                            {
                                RequestId = requestId,
                                IsSuccess = false,
                                Message = $"Error adding product: {ex.Message}"
                            };
                        }
                        break;

                    case "Edit":
                        if (request.ProductId == null)
                        {
                            _logger.LogWarning("Product ID missing for edit request: RequestId={RequestId}", requestId);
                            return new ProcessRequestResponse
                            {
                                RequestId = requestId,
                                IsSuccess = false,
                                Message = "Product ID is required for edit"
                            };
                        }

                        product = await _dbContext.Products
                            .Include(p => p.ProductIngredients)
                            .FirstOrDefaultAsync(p => p.ID == request.ProductId);
                        if (product == null)
                        {
                            _logger.LogWarning("Product not found for edit request: ProductId={ProductId}", request.ProductId);
                            return new ProcessRequestResponse
                            {
                                RequestId = requestId,
                                IsSuccess = false,
                                Message = "Product not found"
                            };
                        }

                        try
                        {
                            if (!string.IsNullOrEmpty(request.ProductName))
                                product.ProductName = request.ProductName;
                            if (!string.IsNullOrEmpty(request.Country))
                                product.Country = request.Country;
                            if (!string.IsNullOrEmpty(request.Category))
                                product.Category = request.Category;
                            if (!string.IsNullOrEmpty(request.Description))
                                product.Description = request.Description;
                            if (!string.IsNullOrEmpty(request.ImageUrl))
                                product.ImageUrl = request.ImageUrl;
                            if (!string.IsNullOrEmpty(request.Barcode))
                                product.Barcode = request.Barcode;

                            product.VerifiedBy = adminUsername;
                            product.UpdatedAt = DateTime.UtcNow;

                            _dbContext.ProductIngredients.RemoveRange(product.ProductIngredients);
                            product.ProductIngredients.Clear();

                            if (finalIngredients != null && finalIngredients.Any())
                            {
                                foreach (var ingredient in finalIngredients)
                                {
                                    var dbIngredient = await _dbContext.Ingredients
                                        .FirstOrDefaultAsync(i => i.Id == ingredient.Id || i.Name.ToLower() == ingredient.Name.ToLower());
                                    if (dbIngredient == null)
                                    {
                                        dbIngredient = new Ingredient
                                        {
                                            Id = ingredient.Id != Guid.Empty ? ingredient.Id : Guid.NewGuid(),
                                            Name = ingredient.Name,
                                            Status = ingredient.Status ?? "Unknown",
                                            ECode = ingredient.ECode ?? "N/A",
                                            Description = ingredient.Description ?? ingredient.Name,
                                            IsHalal = ingredient.IsHalal ?? new List<string> { "None" },
                                            IsHaram = ingredient.IsHaram ?? new List<string> { "None" },
                                            IsMushbooh = ingredient.IsMushbooh ?? new List<string> { "None" },
                                            CreatedAt = ingredient.CreatedAt,
                                            UpdatedAt = ingredient.UpdatedAt ?? DateTime.UtcNow,
                                            ProductIngredients = new List<ProductIngredient>()
                                        };
                                        _dbContext.Ingredients.Add(dbIngredient);
                                        await _dbContext.SaveChangesAsync();
                                    }

                                    var productIngredient = new ProductIngredient
                                    {
                                        ProductId = product.ID,
                                        IngredientId = dbIngredient.Id,
                                        CreatedAt = DateTime.UtcNow
                                    };
                                    _dbContext.ProductIngredients.Add(productIngredient);
                                }
                            }

                            await _dbContext.SaveChangesAsync();

                            // Update product status
                            var evaluationResponse = await _ingredientService.EvaluateProductStatusAsync(product.ID);
                            product.Status = evaluationResponse.Status;
                            _dbContext.Products.Update(product);
                            await _dbContext.SaveChangesAsync();
                        }
                        catch (DbUpdateException ex)
                        {
                            _logger.LogError(ex, "Database error updating product for RequestId={RequestId}: {Error}",
                                requestId, ex.InnerException?.Message ?? ex.Message);
                            await transaction.RollbackAsync();
                            return new ProcessRequestResponse
                            {
                                RequestId = requestId,
                                IsSuccess = false,
                                Message = $"Error updating product: {ex.Message}"
                            };
                        }
                        break;

                    case "Delete":
                        if (request.ProductId == null)
                        {
                            _logger.LogWarning("Product ID missing for delete request: RequestId={RequestId}", requestId);
                            return new ProcessRequestResponse
                            {
                                RequestId = requestId,
                                IsSuccess = false,
                                Message = "Product ID is required for delete"
                            };
                        }

                        product = await _dbContext.Products
                            .Include(p => p.ProductIngredients)
                            .FirstOrDefaultAsync(p => p.ID == request.ProductId);
                        if (product == null)
                        {
                            _logger.LogWarning("Product not found for delete request: ProductId={ProductId}", request.ProductId);
                            return new ProcessRequestResponse
                            {
                                RequestId = requestId,
                                IsSuccess = false,
                                Message = "Product not found"
                            };
                        }

                        try
                        {
                            _dbContext.ProductIngredients.RemoveRange(product.ProductIngredients);
                            _dbContext.Products.Remove(product);
                            await _dbContext.SaveChangesAsync();
                        }
                        catch (DbUpdateException ex)
                        {
                            _logger.LogError(ex, "Database error deleting product for RequestId={RequestId}: {Error}",
                                requestId, ex.InnerException?.Message ?? ex.Message);
                            await transaction.RollbackAsync();
                            return new ProcessRequestResponse
                            {
                                RequestId = requestId,
                                IsSuccess = false,
                                Message = $"Error deleting product: {ex.Message}"
                            };
                        }
                        break;
                }

                request.RequestStatus = "Approved";
                request.ActionDate = DateTime.UtcNow;
                request.ActionedBy = adminUsername;

                try
                {
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                    await _cacheService.RemoveAsync("pending_requests");

                    _logger.LogInformation("Request {RequestType} approved successfully: RequestId={RequestId}", request.RequestType, requestId);
                    return new ProcessRequestResponse
                    {
                        RequestId = requestId,
                        IsSuccess = true,
                        Message = $"Request {request.RequestType} approved successfully",
                        Request = request,
                        Product = product,
                        Ingredients = finalIngredients ?? new List<Ingredient>()
                    };
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error updating request status for RequestId={RequestId}: {Error}",
                        requestId, ex.InnerException?.Message ?? ex.Message);
                    await transaction.RollbackAsync();
                    return new ProcessRequestResponse
                    {
                        RequestId = requestId,
                        IsSuccess = false,
                        Message = $"Request approved, but failed to update status: {ex.Message}",
                        Request = request,
                        Product = product,
                        Ingredients = finalIngredients ?? new List<Ingredient>()
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error approving request: RequestId={RequestId}", requestId);
                await transaction.RollbackAsync();
                return new ProcessRequestResponse
                {
                    RequestId = requestId,
                    IsSuccess = false,
                    Message = $"Error approving request: {ex.Message}",
                    Request = null,
                    Ingredients = new List<Ingredient>()
                };
            }
        }

        public async Task<ProcessRequestResponse> RejectRequest(Guid requestId, string adminUsername, string rejectionReason)
        {
            try
            {
                var request = await _dbContext.ProductChangeRequests
                    .FirstOrDefaultAsync(r => r.RequestId == requestId);

                if (request == null)
                {
                    _logger.LogWarning("Reject request not found: RequestId={RequestId}", requestId);
                    return new ProcessRequestResponse
                    {
                        RequestId = requestId,
                        IsSuccess = false,
                        Message = "Request not found"
                    };
                }

                if (request.RequestStatus != "Pending")
                {
                    _logger.LogWarning("Request already processed: RequestId={RequestId}, Status={Status}", requestId, request.RequestStatus);
                    return new ProcessRequestResponse
                    {
                        RequestId = requestId,
                        IsSuccess = false,
                        Message = "Request is already processed"
                    };
                }

                if (string.IsNullOrWhiteSpace(rejectionReason))
                {
                    _logger.LogWarning("Rejection reason missing for request: RequestId={RequestId}", requestId);
                    return new ProcessRequestResponse
                    {
                        RequestId = requestId,
                        IsSuccess = false,
                        Message = "Rejection reason is required"
                    };
                }

                request.RequestStatus = "Rejected";
                request.ActionDate = DateTime.UtcNow;
                request.ActionedBy = adminUsername;
                request.RejectionReason = rejectionReason;

                await _dbContext.SaveChangesAsync();
                await _cacheService.RemoveAsync("pending_requests");

                _logger.LogInformation("Request rejected successfully: RequestId={RequestId}", requestId);
                return new ProcessRequestResponse
                {
                    RequestId = requestId,
                    IsSuccess = true,
                    Message = "Request rejected successfully",
                    Request = request
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting request: RequestId={RequestId}", requestId);
                return new ProcessRequestResponse
                {
                    RequestId = requestId,
                    IsSuccess = false,
                    Message = $"Error rejecting request: {ex.Message}"
                };
            }
        }
        public async Task<List<ProductChangeRequest>> GetAllRequests()
        {
            try
            {
                _logger.LogInformation("Fetching all product change requests from database.");
                var requests = await _requestRepository.GetAllRequests();
                _logger.LogInformation("Database returned {Count} requests.", requests.Count);
                return requests;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all product change requests");
                return new List<ProductChangeRequest>();
            }
        }


        public async Task<List<ProductChangeRequest>> GetUserRequests(string username)
        {
            return await _dbContext.ProductChangeRequests
                .Where(r => r.RequestedBy == username)
                .Include(r => r.OriginalProduct)
                .ToListAsync();
        }

        public async Task<ProductChangeRequest?> GetRequestDetails(Guid requestId)
        {
            return await _dbContext.ProductChangeRequests
                .Include(r => r.OriginalProduct)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);
        }
    }
}