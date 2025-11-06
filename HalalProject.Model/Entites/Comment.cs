using System;
using System.ComponentModel.DataAnnotations;
using HalalProject.Model.Entites;

namespace HalalProject.Model.Entities
{
    public class Comment
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Type { get; set; } = string.Empty; // "Product" or "Ingredient"

        public Guid? ProductId { get; set; }
        public Product? Product { get; set; }

        public Guid? IngredientId { get; set; }
        public Ingredient? Ingredient { get; set; }

        [Required]
        public int UserId { get; set; } 
        public UserModel User { get; set; } = null!;

        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}