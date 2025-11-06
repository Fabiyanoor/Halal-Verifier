using System;
using System.ComponentModel.DataAnnotations;

namespace HalalProject.Model.Entites
{
    public class Poll
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Type { get; set; } = string.Empty; // "Product" or "Ingredient"

        public Guid? ProductId { get; set; }
        public Product? Product { get; set; }

        public Guid? IngredientId { get; set; }
        public Ingredient? Ingredient { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime ExpiresAt { get; set; } // Poll expiration date

        public bool IsActive { get; set; } = true; // Poll status

        public List<Vote> Votes { get; set; } = new List<Vote>(); // Votes cast in the poll
    }
}