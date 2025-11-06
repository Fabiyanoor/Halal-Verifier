using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HalalProject.Model.Entites
{
    public class ProductChangeRequest
    {
        [Key]
        public Guid RequestId { get; set; } = Guid.NewGuid();
        public string? RequestType { get; set; } = string.Empty; // "Add", "Edit", "Delete"
        public string? RequestStatus { get; set; } = "Pending"; // "Pending", "Approved", "Rejected"
        public string? RequestedBy { get; set; } = string.Empty; // Username of requester
        public DateTime RequestDate { get; set; } = DateTime.UtcNow;
        public DateTime? ActionDate { get; set; }
        public string? ActionedBy { get; set; } // Admin who approved/rejected
        public string? RejectionReason { get; set; }

        // For Add/Edit requests
        public string? ProductName { get; set; }
        public string? Category { get; set; }
        public string? Status { get; set; } // Later decided by admin
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string ?Barcode { get; set; }
        public string? Country { get; set; } // Added for country-specific ingredients
        public bool UseOnlyUserIngredients { get; set; } = false; // New field
        public List<string> Ingredients { get; set; } = new List<string>(); // User-provided ingredients (optional)

        // For Edit/Delete requests
        public Guid? ProductId { get; set; }
        public Product OriginalProduct { get; set; }
    }

    public enum RequestStatus
    {
        Pending,
        Approved,
        Rejected
    }
}