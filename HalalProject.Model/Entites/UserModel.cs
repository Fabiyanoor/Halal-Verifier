using HalalProject.Model.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HalalProject.Model.Entites
{
    public class UserModel
    {
        public UserModel()
        {
            UserRoles = new List<UserRoleModel>();
            Votes = new List<Vote>();
            Comments = new List<Comment>();
        }

        public int ID { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public virtual ICollection<UserRoleModel> UserRoles { get; set; }
        public virtual ICollection<Vote> Votes { get; set; } // Added for votes
        public virtual ICollection<Comment> Comments { get; set; } // Added for comments
    }
}