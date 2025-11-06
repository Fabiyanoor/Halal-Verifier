using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;

namespace HalalProject.Model.DTO
{
    public class ProductCreateDto
    {
        public string ProductName { get; set; }
        public string Country { get; set; }
        public string Description { get; set; }
        public string Barcode { get; set; }
        public string Category { get; set; }
        public List<string> IngredientNames { get; set; }
        public bool UseAI { get; set; }
        public IFormFile Image { get; set; } // Added for image upload}



    }
}
