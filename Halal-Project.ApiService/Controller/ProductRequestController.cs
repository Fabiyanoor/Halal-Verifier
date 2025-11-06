using HalalProject.Model.DTO;
using HalalProject.Model.Entites;
using HalalProject.Model.Models;
using Halal_project_BL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HalalProject.ApiService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductChangeRequestController : ControllerBase
    {
        private readonly IProductChangeRequestService _requestService;
        private readonly ILogger<ProductChangeRequestController> _logger;
        private readonly IWebHostEnvironment _env;

        public ProductChangeRequestController(
            IProductChangeRequestService requestService,
            ILogger<ProductChangeRequestController> logger,
            IWebHostEnvironment env)
        {
            _requestService = requestService ?? throw new ArgumentNullException(nameof(requestService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _env = env ?? throw new ArgumentNullException(nameof(env));
        }

        [HttpPost("create")]
        public async Task<ActionResult<ProductRequestResponse>> CreateProductRequest([FromForm] ProductCreateRequestDTO requestDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Model validation failed: {Errors}", string.Join("; ", errors));
                return BadRequest(new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = "Invalid request data: " + string.Join("; ", errors)
                });
            }

            if (string.IsNullOrEmpty(requestDto.ProductName) || string.IsNullOrEmpty(requestDto.Country))
            {
                _logger.LogWarning("Product name and country are required");
                return BadRequest(new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = "Product name and country are required"
                });
            }

            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    _logger.LogWarning("User not authenticated for product creation request");
                    return Unauthorized(new ProductRequestResponse { IsSuccess = false, Message = "User not authenticated" });
                }

                _logger.LogInformation("Processing create request for ProductName: {ProductName}, Country: {Country}, UseOnlyUserIngredients: {UseOnlyUserIngredients}",
                    requestDto.ProductName, requestDto.Country, requestDto.UseOnlyUserIngredients);

                string imageUrl = null;
                if (requestDto.Image != null && requestDto.Image.Length > 0)
                {
                    var validExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    var extension = Path.GetExtension(requestDto.Image.FileName).ToLower();
                    if (!validExtensions.Contains(extension))
                    {
                        _logger.LogWarning("Invalid image extension: {Extension}", extension);
                        return BadRequest(new ProductRequestResponse
                        {
                            IsSuccess = false,
                            Message = "Only JPG and PNG images are allowed"
                        });
                    }
                    if (requestDto.Image.Length > 5 * 1024 * 1024)
                    {
                        _logger.LogWarning("Image size exceeds 5MB limit");
                        return BadRequest(new ProductRequestResponse
                        {
                            IsSuccess = false,
                            Message = "Image size must be less than 5MB"
                        });
                    }

                    var uploadsFolder = Path.Combine(_env.WebRootPath, "Assets");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await requestDto.Image.CopyToAsync(fileStream);
                    }

                    imageUrl = $"/Assets/{uniqueFileName}";
                }

                var request = new ProductChangeRequest
                {
                    ProductName = requestDto.ProductName,
                    Country = requestDto.Country,
                    Category = requestDto.Category,
                    Description = requestDto.Description,
                    Barcode = requestDto.Barcode,
                    ImageUrl = imageUrl,
                    Ingredients = requestDto.Ingredients,
                    UseOnlyUserIngredients = requestDto.UseOnlyUserIngredients
                };

                var response = await _requestService.CreateProductRequest(request, username);
                _logger.LogInformation("Product creation request submitted successfully: RequestId={RequestId}", response.RequestId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product request for ProductName: {ProductName}", requestDto.ProductName);
                return StatusCode(500, new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = $"An error occurred: {ex.Message}"
                });
            }
        }

        [HttpPost("update")]
        public async Task<ActionResult<ProductRequestResponse>> UpdateProductRequest([FromForm] UpdateProductRequestDto requestDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Model validation failed: {Errors}", string.Join("; ", errors));
                return BadRequest(new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = "Invalid request data: " + string.Join("; ", errors)
                });
            }

            if (requestDto.ProductId == Guid.Empty || string.IsNullOrEmpty(requestDto.Country))
            {
                _logger.LogWarning("Product ID and country are required for update request");
                return BadRequest(new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = "Product ID and country are required"
                });
            }

            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    _logger.LogWarning("User not authenticated for update request");
                    return Unauthorized(new ProductRequestResponse { IsSuccess = false, Message = "User not authenticated" });
                }

                _logger.LogInformation("Processing update request for ProductId: {ProductId}, Country: {Country}, UseOnlyUserIngredients: {UseOnlyUserIngredients}",
                    requestDto.ProductId, requestDto.Country, requestDto.UseOnlyUserIngredients);

                string imageUrl = null;
                if (requestDto.Image != null && requestDto.Image.Length > 0)
                {
                    var validExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    var extension = Path.GetExtension(requestDto.Image.FileName).ToLower();
                    if (!validExtensions.Contains(extension))
                    {
                        _logger.LogWarning("Invalid image extension: {Extension}", extension);
                        return BadRequest(new ProductRequestResponse
                        {
                            IsSuccess = false,
                            Message = "Only JPG and PNG images are allowed"
                        });
                    }
                    if (requestDto.Image.Length > 5 * 1024 * 1024)
                    {
                        _logger.LogWarning("Image size exceeds 5MB limit");
                        return BadRequest(new ProductRequestResponse
                        {
                            IsSuccess = false,
                            Message = "Image size must be less than 5MB"
                        });
                    }

                    var uploadsFolder = Path.Combine(_env.WebRootPath, "Assets");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await requestDto.Image.CopyToAsync(fileStream);
                    }

                    imageUrl = $"/Assets/{uniqueFileName}";
                    requestDto.ImageUrl = imageUrl;
                }

                var response = await _requestService.UpdateProductRequest(requestDto, username);
                _logger.LogInformation("Product update request submitted successfully: RequestId={RequestId}", response.RequestId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating update request for ProductId: {ProductId}", requestDto.ProductId);
                return StatusCode(500, new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = $"An error occurred: {ex.Message}"
                });
            }
        }

        [HttpPost("edit")]
        public async Task<ActionResult<ProductRequestResponse>> EditProductRequest([FromForm] EditProductRequestDto requestDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Model validation failed: {Errors}", string.Join("; ", errors));
                return BadRequest(new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = "Invalid request data: " + string.Join("; ", errors)
                });
            }

            if (requestDto.RequestId == Guid.Empty || string.IsNullOrEmpty(requestDto.Country))
            {
                _logger.LogWarning("Request ID and country are required for edit request");
                return BadRequest(new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = "Request ID and country are required"
                });
            }

            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    _logger.LogWarning("User not authenticated for edit request");
                    return Unauthorized(new ProductRequestResponse { IsSuccess = false, Message = "User not authenticated" });
                }

                _logger.LogInformation("Processing edit request for RequestId: {RequestId}, Country: {Country}, UseOnlyUserIngredients: {UseOnlyUserIngredients}",
                    requestDto.RequestId, requestDto.Country, requestDto.UseOnlyUserIngredients);

                string imageUrl = null;
                if (requestDto.Image != null && requestDto.Image.Length > 0)
                {
                    var validExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    var extension = Path.GetExtension(requestDto.Image.FileName).ToLower();
                    if (!validExtensions.Contains(extension))
                    {
                        _logger.LogWarning("Invalid image extension: {Extension}", extension);
                        return BadRequest(new ProductRequestResponse
                        {
                            IsSuccess = false,
                            Message = "Only JPG and PNG images are allowed"
                        });
                    }
                    if (requestDto.Image.Length > 5 * 1024 * 1024)
                    {
                        _logger.LogWarning("Image size exceeds 5MB limit");
                        return BadRequest(new ProductRequestResponse
                        {
                            IsSuccess = false,
                            Message = "Image size must be less than 5MB"
                        });
                    }

                    var uploadsFolder = Path.Combine(_env.WebRootPath, "Assets");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await requestDto.Image.CopyToAsync(fileStream);
                    }

                    imageUrl = $"/Assets/{uniqueFileName}";
                    requestDto.ImageUrl = imageUrl;
                }

                var response = await _requestService.EditUserRequest(requestDto, username);
                _logger.LogInformation("Product edit request submitted successfully: RequestId={RequestId}", response.RequestId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing request for RequestId: {RequestId}", requestDto.RequestId);
                return StatusCode(500, new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = $"An error occurred: {ex.Message}"
                });
            }
        }

        [HttpPost("delete/{productId}")]
        public async Task<ActionResult<ProductRequestResponse>> DeleteProductRequest(Guid productId)
        {
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    _logger.LogWarning("User not authenticated for delete request {ProductId}", productId);
                    return Unauthorized(new ProductRequestResponse { IsSuccess = false, Message = "User not authenticated" });
                }

                _logger.LogInformation("Processing delete request for ProductId: {ProductId}", productId);
                var response = await _requestService.DeleteProductRequest(productId, username);
                if (!response.IsSuccess)
                {
                    _logger.LogWarning("Delete request failed: ProductId={ProductId}, Message={Message}", productId, response.Message);
                    return BadRequest(response);
                }

                _logger.LogInformation("Product deletion request submitted successfully: RequestId={RequestId}", response.RequestId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product request for ProductId: {ProductId}", productId);
                return StatusCode(500, new ProductRequestResponse
                {
                    IsSuccess = false,
                    Message = $"An error occurred: {ex.Message}"
                });
            }
        }

        [HttpPost("verify-ingredients/{requestId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<Ingredient>>> VerifyIngredients(Guid requestId)
        {
            try
            {
                _logger.LogInformation("Verifying ingredients for RequestId={RequestId}", requestId);
                var response = await _requestService.VerifyIngredients(requestId);
                _logger.LogInformation("Ingredients verified successfully: RequestId={RequestId}, IngredientCount={Count}", requestId, response.Count);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying ingredients for RequestId={RequestId}", requestId);
                return StatusCode(500, new List<Ingredient>());
            }
        }

        [HttpPost("approve/{requestId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ProcessRequestResponse>> ApproveRequest(Guid requestId, [FromBody] List<IngredientDTO> finalIngredients)
        {
            try
            {
                var adminUsername = User.Identity?.Name;
                if (string.IsNullOrEmpty(adminUsername))
                {
                    _logger.LogWarning("Admin not authenticated for approve request {RequestId}", requestId);
                    return Unauthorized(new ProcessRequestResponse
                    {
                        IsSuccess = false,
                        Message = "Admin not authenticated"
                    });
                }

                _logger.LogInformation("Approving request {RequestId} with {IngredientCount} final ingredients", requestId, finalIngredients?.Count ?? 0);

                // Map IngredientDTO to Ingredient
                var ingredients = finalIngredients?.Select(dto => new Ingredient
                {
                    Id = dto.Id,
                    Name = dto.Name,
                    Status = dto.Status,
                    ECode = dto.ECode,
                    Description = dto.Description,
                    IsHalal = dto.IsHalal,
                    IsHaram = dto.IsHaram,
                    IsMushbooh = dto.IsMushbooh,
                    CreatedAt = dto.CreatedAt,
                    UpdatedAt = dto.UpdatedAt,
                    ProductIngredients = new List<ProductIngredient>() // Managed server-side
                }).ToList() ?? new List<Ingredient>();

                var response = await _requestService.ApproveRequest(requestId, adminUsername, ingredients);
                if (!response.IsSuccess)
                {
                    _logger.LogWarning("Approve request failed: RequestId={RequestId}, Message={Message}", requestId, response.Message);
                    return BadRequest(response);
                }

                _logger.LogInformation("Request approved successfully: RequestId={RequestId}", requestId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving request {RequestId}", requestId);
                return StatusCode(500, new ProcessRequestResponse
                {
                    IsSuccess = false,
                    Message = $"An error occurred: {ex.Message}"
                });
            }
        }

        [HttpPost("reject/{requestId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ProcessRequestResponse>> RejectRequest(Guid requestId, [FromBody] RejectionReasonDTO rejection)
        {
            try
            {
                var adminUsername = User.Identity?.Name;
                if (string.IsNullOrEmpty(adminUsername))
                {
                    _logger.LogWarning("Admin not authenticated for reject request {RequestId}", requestId);
                    return Unauthorized(new ProcessRequestResponse { IsSuccess = false, Message = "Admin not authenticated" });
                }

                _logger.LogInformation("Rejecting request {RequestId}", requestId);
                var response = await _requestService.RejectRequest(requestId, adminUsername, rejection.Reason);
                if (!response.IsSuccess)
                {
                    _logger.LogWarning("Reject request failed: RequestId={RequestId}, Message={Message}", requestId, response.Message);
                    return BadRequest(response);
                }

                _logger.LogInformation("Request rejected successfully: RequestId={RequestId}", requestId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting request {RequestId}", requestId);
                return StatusCode(500, new ProcessRequestResponse
                {
                    IsSuccess = false,
                    Message = $"An error occurred: {ex.Message}"
                });
            }
        }

        [HttpGet("all")]
      
        public async Task<IActionResult> GetAllRequests()
        {
            try
            {
                _logger.LogInformation("Fetching all product change requests.");
                var requests = await _requestService.GetAllRequests();
                return Ok(new ApiResponse<List<ProductChangeRequest>>
                {
                    Success = true,
                    Data = requests
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all product change requests");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    ErrorMessage = "An error occurred while fetching all requests"
                });
            }
        }

        [HttpGet("my-requests")]
        public async Task<ActionResult<BaseResponseModel>> GetUserRequests()
        {
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    _logger.LogWarning("User not authenticated for retrieving user requests");
                    return Unauthorized(new BaseResponseModel { Success = false, ErrorMessage = "User not authenticated" });
                }

                var requests = await _requestService.GetUserRequests(username);
                return Ok(new BaseResponseModel { Success = true, Data = requests });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user requests");
                return StatusCode(500, new BaseResponseModel { Success = false, ErrorMessage = $"An error occurred: {ex.Message}" });
            }
        }

        [HttpGet("details/{requestId}")]
        public async Task<ActionResult<BaseResponseModel>> GetRequestDetails(Guid requestId)
        {
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    _logger.LogWarning("User not authenticated for retrieving request details {RequestId}", requestId);
                    return Unauthorized(new BaseResponseModel { Success = false, ErrorMessage = "User not authenticated" });
                }

                var request = await _requestService.GetRequestDetails(requestId);
                if (request == null)
                {
                    _logger.LogWarning("Request not found: RequestId={RequestId}", requestId);
                    return Ok(new BaseResponseModel { Success = false, ErrorMessage = "Request not found" });
                }

                if (User.IsInRole("Admin") || request.RequestedBy == username)
                    return Ok(new BaseResponseModel { Success = true, Data = request });

                _logger.LogWarning("Unauthorized access to request details {RequestId} by user {Username}", requestId, username);
                return StatusCode(403, new BaseResponseModel { Success = false, ErrorMessage = "Unauthorized access to request details" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving request details {RequestId}", requestId);
                return StatusCode(500, new BaseResponseModel { Success = false, ErrorMessage = $"An error occurred: {ex.Message}" });
            }
        }
    }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public T Data { get; set; }
}