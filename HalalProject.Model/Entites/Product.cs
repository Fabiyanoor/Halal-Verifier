using HalalProject.Model.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HalalProject.Model.Entites
{
    public class Product
    {
        public Guid ID { get; set; } = Guid.NewGuid();

        [Required]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = "Unknown"; // HALAL/Haram/Mushbooh/Unknown

        [Required]
        public string Category { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public string ImageUrl { get; set; }
        public string Barcode { get; set; }

        public string Country { get; set; } // Country context for ingredients

        public List<string> Ingredients { get; set; } = new List<string>(); // List of ingredient names

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string AddedBy { get; set; }
        public string VerifiedBy { get; set; }

        public List<ProductIngredient> ProductIngredients { get; set; } = new List<ProductIngredient>();
        public List<Poll> Polls { get; set; } = new List<Poll>(); // Added for polls
        public List<Comment> Comments { get; set; } = new List<Comment>(); // Added for comments
    }
}