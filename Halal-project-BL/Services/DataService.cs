using HalalProject.Database.Data;
using HalalProject.Model.Entites;
using HalalProject.Model.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Halal_project_BL.Services
{
    public class DataService
    {
        private readonly AppDbContext _context;

        public DataService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Poll> CreatePollAsync(Guid? productId, Guid? ingredientId, int daysValid = 7)
        {
            if (productId == null && ingredientId == null)
                throw new ArgumentException("Either productId or ingredientId must be provided.");

            if (productId.HasValue && !await _context.Products.AnyAsync(p => p.ID == productId))
                throw new ArgumentException("Invalid product ID.");

            if (ingredientId.HasValue && !await _context.Ingredients.AnyAsync(i => i.Id == ingredientId))
                throw new ArgumentException("Invalid ingredient ID.");

            var poll = new Poll
            {
                Type = productId != null ? "Product" : "Ingredient",
                ProductId = productId,
                IngredientId = ingredientId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(daysValid),
                IsActive = true
            };

            _context.Polls.Add(poll);
            await _context.SaveChangesAsync();
            return poll;
        }

        public async Task<Vote> CastVoteAsync(Guid pollId, int userId, string status)
        {
            if (!await _context.Users.AnyAsync(u => u.ID == userId))
                throw new ArgumentException("Invalid user ID.");

            var poll = await _context.Polls
                .Include(p => p.Votes)
                .FirstOrDefaultAsync(p => p.Id == pollId && p.IsActive && p.ExpiresAt > DateTime.UtcNow);

            if (poll == null)
                throw new InvalidOperationException("Poll is invalid, expired, or closed.");

            if (!new[] { "Halal", "Haram", "Mushbooh" }.Contains(status))
                throw new ArgumentException("Invalid status. Must be Halal, Haram, or Mushbooh.");

            var existingVote = poll.Votes.FirstOrDefault(v => v.UserId == userId);
            if (existingVote != null)
                throw new InvalidOperationException("User has already voted in this poll.");

            var vote = new Vote
            {
                PollId = pollId,
                UserId = userId,
                Status = status,
                VotedAt = DateTime.UtcNow
            };

            _context.Votes.Add(vote);
            await _context.SaveChangesAsync();

            await RecalculateStatusAsync(poll);
            return vote;
        }

        private async Task RecalculateStatusAsync(Poll poll)
        {
            var votes = await _context.Votes
                .Where(v => v.PollId == poll.Id)
                .GroupBy(v => v.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var maxVotes = votes.OrderByDescending(v => v.Count).FirstOrDefault();
            if (maxVotes == null) return;

            if (poll.Type == "Product" && poll.ProductId.HasValue)
            {
                var product = await _context.Products.FindAsync(poll.ProductId.Value);
                if (product != null)
                {
                    product.Status = maxVotes.Status;
                    product.UpdatedAt = DateTime.UtcNow;
                }
            }
            else if (poll.Type == "Ingredient" && poll.IngredientId.HasValue)
            {
                var ingredient = await _context.Ingredients.FindAsync(poll.IngredientId.Value);
                if (ingredient != null)
                {
                    ingredient.Status = maxVotes.Status;
                    ingredient.UpdatedAt = DateTime.UtcNow;

                    var products = await _context.ProductIngredients
                        .Where(pi => pi.IngredientId == ingredient.Id)
                        .Select(pi => pi.Product)
                        .ToListAsync();

                    foreach (var product in products)
                    {
                        await RecalculateProductStatusAsync(product.ID);
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task RecalculateProductStatusAsync(Guid productId)
        {
            var product = await _context.Products
                .Include(p => p.ProductIngredients)
                .ThenInclude(pi => pi.Ingredient)
                .FirstOrDefaultAsync(p => p.ID == productId);

            if (product == null) return;

            var ingredientStatuses = product.ProductIngredients
                .Select(pi => pi.Ingredient.Status)
                .ToList();

            if (ingredientStatuses.Contains("Haram"))
                product.Status = "Haram";
            else if (ingredientStatuses.Contains("Mushbooh"))
                product.Status = "Mushbooh";
            else if (ingredientStatuses.All(s => s == "Halal"))
                product.Status = "Halal";
            else
                product.Status = "Unknown";

            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<Comment> CreateCommentAsync(Guid? productId, Guid? ingredientId, int userId, string content)
        {
            if (productId == null && ingredientId == null)
                throw new ArgumentException("Either productId or ingredientId must be provided.");

            if (!await _context.Users.AnyAsync(u => u.ID == userId))
                throw new ArgumentException("Invalid user ID.");

            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Comment content cannot be empty.");

            if (productId.HasValue && !await _context.Products.AnyAsync(p => p.ID == productId))
                throw new ArgumentException("Invalid product ID.");

            if (ingredientId.HasValue && !await _context.Ingredients.AnyAsync(i => i.Id == ingredientId))
                throw new ArgumentException("Invalid ingredient ID.");

            var comment = new Comment
            {
                Type = productId.HasValue ? "Product" : "Ingredient",
                ProductId = productId,
                IngredientId = ingredientId,
                UserId = userId,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();
            return comment;
        }

        public async Task DeletePollAsync(Guid pollId)
        {
            var poll = await _context.Polls.FindAsync(pollId);
            if (poll == null)
                throw new InvalidOperationException("Poll not found.");

            poll.IsActive = false;
            await _context.SaveChangesAsync();
        }

        public async Task<List<Ingredient>> GetAllIngredientsAsync()
        {
            return await _context.Ingredients
                .Select(i => new Ingredient
                {
                    Id = i.Id,
                    Name = i.Name,
                    Status = i.Status,
                    ECode = i.ECode,
                    Description = i.Description,
                    IsHalal = i.IsHalal,
                    IsHaram = i.IsHaram,
                    IsMushbooh = i.IsMushbooh,
                    CreatedAt = i.CreatedAt,
                    UpdatedAt = i.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<List<Product>> GetAllProductsAsync()
        {
            return await _context.Products
                .Include(p => p.ProductIngredients)
                .ThenInclude(pi => pi.Ingredient)
                .Select(p => new Product
                {
                    ID = p.ID,
                    ProductName = p.ProductName,
                    Status = p.Status,
                    Category = p.Category,
                    Description = p.Description,
                    ImageUrl = p.ImageUrl,
                    Barcode = p.Barcode,
                    Country = p.Country,
                    Ingredients = p.Ingredients,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    AddedBy = p.AddedBy,
                    VerifiedBy = p.VerifiedBy,
                    ProductIngredients = p.ProductIngredients
                })
                .ToListAsync();
        }
    }
}