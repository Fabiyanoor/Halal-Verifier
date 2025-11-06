using HalalProject.Model.Entites;
using HalalProject.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Newtonsoft.Json;
using System;

namespace HalalProject.Database.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Product> Products { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<ProductIngredient> ProductIngredients { get; set; }
        public DbSet<ProductChangeRequest> ProductChangeRequests { get; set; }
        public DbSet<UserModel> Users { get; set; }
        public DbSet<UserRoleModel> UserRoles { get; set; }
        public DbSet<RoleModel> Roles { get; set; }
        public DbSet<Poll> Polls { get; set; }
        public DbSet<Vote> Votes { get; set; }
        public DbSet<Comment> Comments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ProductChangeRequest Configuration (unchanged)
            modelBuilder.Entity<ProductChangeRequest>(e =>
            {
                e.HasKey(r => r.RequestId);
                e.HasOne(r => r.OriginalProduct)
                    .WithMany()
                    .HasForeignKey(r => r.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.Property(r => r.Country)
                    .IsRequired(false); // Nullable in DB, enforced in service

                e.Property(r => r.UseOnlyUserIngredients)
                    .IsRequired()
                    .HasDefaultValue(false);

                e.Property(r => r.Status)
                    .IsRequired(false); // Nullable, set by admin

                var ingredientsComparer = new ValueComparer<List<string>>(
                    (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                    c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v == null ? 0 : v.GetHashCode())),
                    c => c == null ? new List<string>() : c.ToList());

                e.Property(r => r.Ingredients)
                    .HasConversion(
                        v => JsonConvert.SerializeObject(v),
                        v => JsonConvert.DeserializeObject<List<string>>(v) ?? new List<string>())
                    .Metadata
                    .SetValueComparer(ingredientsComparer);
            });

            // Product Configuration (unchanged)
            modelBuilder.Entity<Product>(e =>
            {
                e.HasKey(p => p.ID);
                e.HasIndex(p => p.ProductName).IsUnique();
                e.Property(p => p.Status).IsRequired().HasDefaultValue("Unknown");
                e.Property(p => p.Country).IsRequired(false); // Nullable in DB, enforced in service
            });

            // Ingredient Configuration (unchanged)
            modelBuilder.Entity<Ingredient>(e =>
            {
                e.HasKey(i => i.Id);
                e.HasIndex(i => i.Name).IsUnique();
                e.Property(i => i.Status).IsRequired().HasDefaultValue("Unknown");

                var countryListComparer = new ValueComparer<List<string>>(
                    (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                    c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v == null ? 0 : v.GetHashCode())),
                    c => c == null ? new List<string>() : c.ToList());

                e.Property(i => i.IsHalal)
                    .HasConversion(
                        v => JsonConvert.SerializeObject(v),
                        v => JsonConvert.DeserializeObject<List<string>>(v) ?? new List<string> { "None" })
                    .Metadata
                    .SetValueComparer(countryListComparer);

                e.Property(i => i.IsHaram)
                    .HasConversion(
                        v => JsonConvert.SerializeObject(v),
                        v => JsonConvert.DeserializeObject<List<string>>(v) ?? new List<string> { "None" })
                    .Metadata
                    .SetValueComparer(countryListComparer);

                e.Property(i => i.IsMushbooh)
                    .HasConversion(
                        v => JsonConvert.SerializeObject(v),
                        v => JsonConvert.DeserializeObject<List<string>>(v) ?? new List<string> { "None" })
                    .Metadata
                    .SetValueComparer(countryListComparer);
            });

            // ProductIngredient Configuration (unchanged)
            modelBuilder.Entity<ProductIngredient>(e =>
            {
                e.HasKey(pi => new { pi.ProductId, pi.IngredientId });
                e.HasOne(pi => pi.Product)
                    .WithMany(p => p.ProductIngredients)
                    .HasForeignKey(pi => pi.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(pi => pi.Ingredient)
                    .WithMany(i => i.ProductIngredients)
                    .HasForeignKey(pi => pi.IngredientId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // UserModel Configuration
            modelBuilder.Entity<UserModel>(e =>
            {
                e.HasKey(u => u.ID);
                e.HasIndex(u => u.Username).IsUnique();
                e.HasIndex(u => u.Email).IsUnique();
                e.Property(u => u.Username).IsRequired();
                e.Property(u => u.Email).IsRequired();
                e.Property(u => u.Password).IsRequired();
            });

            // UserRoleModel Configuration
            modelBuilder.Entity<UserRoleModel>(e =>
            {
                e.HasKey(ur => ur.ID);
                e.HasOne(ur => ur.User)
                    .WithMany(u => u.UserRoles)
                    .HasForeignKey(ur => ur.UserID)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(ur => ur.Role)
                    .WithMany() // RoleModel has no navigation property
                    .HasForeignKey(ur => ur.RoleID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // RoleModel Configuration
            modelBuilder.Entity<RoleModel>(e =>
            {
                e.HasKey(r => r.ID);
                e.HasIndex(r => r.RoleName).IsUnique();
                e.Property(r => r.RoleName).IsRequired();
            });

            // Poll Configuration
            modelBuilder.Entity<Poll>(e =>
            {
                e.HasKey(p => p.Id);
                e.HasOne(p => p.Product)
                    .WithMany(pr => pr.Polls)
                    .HasForeignKey(p => p.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(p => p.Ingredient)
                    .WithMany(i => i.Polls)
                    .HasForeignKey(p => p.IngredientId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.Property(p => p.Type).IsRequired();
                e.Property(p => p.CreatedAt).IsRequired();
                e.Property(p => p.ExpiresAt).IsRequired();
            });

            // Vote Configuration
            modelBuilder.Entity<Vote>(e =>
            {
                e.HasKey(v => v.Id);
                e.HasOne(v => v.Poll)
                    .WithMany(p => p.Votes)
                    .HasForeignKey(v => v.PollId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(v => v.User)
                    .WithMany(u => u.Votes)
                    .HasForeignKey(v => v.UserId)
                    .OnDelete(DeleteBehavior.Restrict); // Restrict to avoid multiple cascade paths
                e.Property(v => v.Status).IsRequired();
                e.HasIndex(v => new { v.PollId, v.UserId }).IsUnique();
            });

            // Comment Configuration
            modelBuilder.Entity<Comment>(e =>
            {
                e.HasKey(c => c.Id);
                e.HasOne(c => c.Product)
                    .WithMany(p => p.Comments)
                    .HasForeignKey(c => c.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(c => c.Ingredient)
                    .WithMany(i => i.Comments)
                    .HasForeignKey(c => c.IngredientId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(c => c.User)
                    .WithMany(u => u.Comments)
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Restrict); // Restrict to avoid multiple cascade paths
                e.Property(c => c.Type).IsRequired();
                e.Property(c => c.Content).IsRequired();
            });
        }
    }
}