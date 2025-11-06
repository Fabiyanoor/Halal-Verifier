using HalalProject.Model.Entities;
using System;
using System.ComponentModel.DataAnnotations;

namespace HalalProject.Model.Entites
{
    public class Vote
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid PollId { get; set; }
        public Poll Poll { get; set; } = null!;

        [Required]
        public int UserId { get; set; } // Changed to int to match UserModel.ID
        public UserModel User { get; set; } = null!;

        [Required]
        public string Status { get; set; } = string.Empty; // "Halal", "Haram", "Mushbooh"

        [Required]
        public DateTime VotedAt { get; set; } = DateTime.UtcNow;
    }
}