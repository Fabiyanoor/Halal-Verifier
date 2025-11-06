using System;

namespace HalalProject.Model.Entites
{
    public class ProductIngredient
    {
        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public Guid IngredientId { get; set; }
        public Ingredient Ingredient { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}