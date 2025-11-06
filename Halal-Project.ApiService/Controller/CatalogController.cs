using System;
using System.Threading.Tasks;
using Halal_project_BL.Services;
using HalalProject.Model.Entites;
using Microsoft.AspNetCore.Mvc;

namespace HalalProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CatalogController : ControllerBase
    {
        private readonly DataService _dataService;

        public CatalogController(DataService dataService)
        {
            _dataService = dataService;
        }

        [HttpGet("ingredients")]
        public async Task<IActionResult> GetAllIngredients()
        {
            try
            {
                var ingredients = await _dataService.GetAllIngredientsAsync();
                return Ok(ingredients);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetAllProducts()
        {
            try
            {
                var products = await _dataService.GetAllProductsAsync();
                return Ok(products);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }
}