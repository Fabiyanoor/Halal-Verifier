using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalalProject.Model.Entites;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;

namespace HalalProject.Model.DTO
{
   
        public class ProductUpdateDto
        {
        public string ProductName { get; set; }
        public string Country { get; set; }
        public string Description { get; set; }
        public string Barcode { get; set; }
        public string Category { get; set; }
        public List<string> IngredientNames { get; set; }
        public bool UseAI { get; set; }
        public IFormFile Image { get; set; } // Added for image upload

    }
    public class IngredientFetchResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<Ingredient> Ingredients { get; set; } = new List<Ingredient>(); // AI or verified user ingredients
        public List<Ingredient> UserIngredients { get; set; } = new List<Ingredient>(); // User-provided ingredients
        public string ProductStatus { get; set; } = string.Empty; // Optional product status
    }
    public class ProductWithIngredientsDto
    {
        [Required]
        public string ProductName { get; set; }

        [Required]
        public string Country { get; set; }

        [Required]
        public string Category { get; set; }

        [Required]
        public string Description { get; set; }

        public string Barcode { get; set; }

        [Required]
        public IFormFile Image { get; set; }  // Changed to required

        public List<IngredientDto> Ingredients { get; set; } = new();
    }

    public class IngredientDto
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Status { get; set; } // "Halal", "Haram", "Mushbooh"

        public string ECode { get; set; }
        public string Description { get; set; }
    }

}
