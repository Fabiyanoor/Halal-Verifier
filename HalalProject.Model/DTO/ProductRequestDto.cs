using HalalProject.Model.Entites;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HalalProject.Model.DTO
{
    public class ProductCreateRequestDTO
    {
        [Required(ErrorMessage = "Product name is required")]
        public string ProductName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Country is required")]
        public string Country { get; set; } = string.Empty;

        public string Category { get; set; }
        public string Description { get; set; }
        public string Barcode { get; set; }
        public IFormFile Image { get; set; }
        public List<string> Ingredients { get; set; } = new List<string>();
        public bool UseOnlyUserIngredients { get; set; } = false;
    }

    public class UpdateProductRequestDto
    {
        [Required(ErrorMessage = "Product ID is required")]
        public Guid ProductId { get; set; }

        public string ProductName { get; set; }
        [Required(ErrorMessage = "Country is required")]
        public string Country { get; set; } = string.Empty;
        public string Category { get; set; }
        public string Description { get; set; }
        public string Barcode { get; set; }
        public IFormFile Image { get; set; }
        public string        ImageUrl { get; set; }
        public List<string> Ingredients { get; set; } = new List<string>();
        public bool UseOnlyUserIngredients { get; set; } = false;
    }

    public class EditProductRequestDto
    {
        [Required(ErrorMessage = "Request ID is required")]
        public Guid RequestId { get; set; }
        public string ProductName { get; set; }
        [Required(ErrorMessage = "Country is required")]
        public string Country { get; set; } = string.Empty;
        public string Category { get; set; }
        public string Description { get; set; }
        public string Barcode { get; set; }
        public IFormFile Image { get; set; }
        public string ImageUrl { get; set; }
        public string Status { get; set; }
        public List<string> Ingredients { get; set; } = new List<string>();
        public bool UseOnlyUserIngredients { get; set; } = false;
    }
    public class ProductViewModel
    {
        public Guid ID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string AddedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<string> Ingredients { get; set; } = new();
    }
    public class ProductChangeRequestDto
    {
        public Guid ProductId { get; set; }
        public Guid RequestId { get; set; }
        public string ProductName { get; set; }
        public string Barcode { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public string Status { get; set; }
        public List<string> Ingredients { get; set; } = new List<string>();
        public bool UseOnlyUserIngredients { get; set; } = false;
    }

    public class ProcessRequestResponse
    {
        public Guid RequestId { get; set; }
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public ProductChangeRequest Request { get; set; }
        public Product Product { get; set; }
        public List<Ingredient> Ingredients { get; set; } = new List<Ingredient>();
    }

    public class RejectionReasonDTO
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class ProductRequestResponse
    {
        public Guid RequestId { get; set; }
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public ProductChangeRequest Request { get; set; }
    }

    public class ProductChangeRequestUpdateDTO
    {
        [Required(ErrorMessage = "Request ID is required")]
        public Guid RequestId { get; set; }
        public Guid ProductId { get; set; }
        [Required(ErrorMessage = "Product name is required")]
        public string ProductName { get; set; } = string.Empty;
        public string Barcode { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public string Status { get; set; }
        public List<string> Ingredients { get; set; } = new List<string>();
    }
    public class ProductWithImageUrlDto
    {
        public string ProductName { get; set; }
        public string Country { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Barcode { get; set; }
        public string Status { get; set; }
        public string ImageUrl { get; set; }
        public List<IngredientDto> Ingredients { get; set; } = new();
    }
    public class ProcessRequestDTO
    {
        public Guid RequestId { get; set; }
        public bool Approve { get; set; }
        public string RejectionReason { get; set; }
    }

    public class IngredientDTO
    {
        public Guid Id { get; set; }
        [Required]
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = "Unknown";
        public string ECode { get; set; }
        public string Description { get; set; }
        public List<string> IsHalal { get; set; } = new List<string> { "None" };
        public List<string> IsHaram { get; set; } = new List<string> { "None" };
        public List<string> IsMushbooh { get; set; } = new List<string> { "None" };
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
    // Add these models to your DTOs
    public class ProductEvaluationRequest
    {
        public string ProductName { get; set; }
        public string Country { get; set; }
        public List<IngredientStatusDto> Ingredients { get; set; } = new();
    }

    public class IngredientStatusDto
    {
        public string Name { get; set; }
        public string Status { get; set; } // Halal, Haram, Mushbooh
    }

    public class ProductEvaluationResponse
    {
        public string Status { get; set; } // Overall product status
        public List<IngredientEvaluationDetail> EvaluationDetails { get; set; }
    }

    public class IngredientEvaluationDetail
    {
        public string IngredientName { get; set; }
        public string Status { get; set; }
        public string Description { get; set; }
    }
}