using HalalProject.Model.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HalalProject.Model.Entites
{
    public class Ingredient
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Name { get; set; } = string.Empty;

        public string Status { get; set; } = "Unknown"; // Halal, Haram, Mushbooh, or Unknown for specific context

        public string ECode { get; set; } // E-code (e.g., E471)

        public string Description { get; set; } // Description of the ingredient

        public List<string> IsHalal { get; set; } = new List<string> { "None" }; // Countries where ingredient is Halal

        public List<string> IsHaram { get; set; } = new List<string> { "None" }; // Countries where ingredient is Haram

        public List<string> IsMushbooh { get; set; } = new List<string> { "None" }; // Countries where ingredient is Mushbooh

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<ProductIngredient> ProductIngredients { get; set; } = new List<ProductIngredient>();
        public List<Poll> Polls { get; set; } = new List<Poll>(); // Added for polls
        public List<Comment> Comments { get; set; } = new List<Comment>(); // Added for comments
    }
}