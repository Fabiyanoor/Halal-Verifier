using System;
using System.Collections.Generic;
using HalalProject.Model.Entites;

namespace HalalProject.Model.Models
{
    public class ProductRequest
    {
        public string ProductName { get; set; }
        public string Barcode { get; set; }
        public string Country { get; set; } // Added country field
    }

    public class IngredientListRequest
    {
        public List<string> Ingredients { get; set; }
        public string Country {get;set; }
    }

    public class ProductResponse
    {
        public List<Ingredient> Ingredients { get; set; }
        public string Country { get; set; } // Added to reflect country context
    }

    public class IngredientListResponse
    {
        public List<Ingredient> Ingredients { get; set; }
    }

    public class ProductEvaluationResponse
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string Status { get; set; }
        public string Country { get; set; } // Added to reflect country context
        public List<Ingredient> Ingredients { get; set; }
    }

    public class RequestEvaluationResponse
    {
        public Guid RequestId { get; set; }
        public string ProductName { get; set; }
        public string Status { get; set; }
        public string Country { get; set; } // Added to reflect country context
        public List<Ingredient> Ingredients { get; set; }
    }

    public class GoogleAIResponse
    {
        public List<Candidate> candidates { get; set; }

        public class Candidate
        {
            public Content content { get; set; }
        }

        public class Content
        {
            public List<Part> parts { get; set; }
        }

        public class Part
        {
            public string text { get; set; }
        }
    }
}