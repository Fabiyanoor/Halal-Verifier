using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HalalProject.Model.Entites;
using HalalProject.Model.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Halal_project_BL.Repositories
{
    public interface IIngredientRepository
    {
        Task<ProductResponse> GetIngredientsAsync(ProductRequest request);
        Task<IngredientListResponse> GetIngredientStatusesAsync(IngredientListRequest request);
    }

    public class IngredientRepository : IIngredientRepository
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiEndpoint;
        private readonly ILogger<IngredientRepository> _logger;

        public IngredientRepository(HttpClient httpClient, IConfiguration configuration, ILogger<IngredientRepository> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = configuration["GoogleAI:ApiKey"] ?? throw new ArgumentNullException("GoogleAI:ApiKey is missing in configuration.");
            _apiEndpoint = configuration["GoogleAI:ApiEndpoint"] ?? throw new ArgumentNullException("GoogleAI:ApiEndpoint is missing in configuration.");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ProductResponse> GetIngredientsAsync(ProductRequest request)
        {
            if (request == null || (string.IsNullOrEmpty(request.ProductName) && string.IsNullOrEmpty(request.Barcode)))
            {
                _logger.LogWarning("Invalid request: Product name or barcode is required.");
                throw new ArgumentException("Product name or barcode is required.");
            }

            if (string.IsNullOrEmpty(request.Country))
            {
                _logger.LogWarning("Country is required for accurate ingredient fetching.");
                throw new ArgumentException("Country is required.");
            }

            // Determine if the product is likely a meat or single-ingredient product
            bool isMeatProduct = IsMeatProduct(request.ProductName);

            // Construct the query based on product type
            string query = !string.IsNullOrEmpty(request.ProductName)
                ? isMeatProduct
                    ? $"Provide the ingredient for the product '{request.ProductName}' sold in {request.Country}, treating it as a single-ingredient meat product. Return the ingredient as 'Ingredient: {request.ProductName}: N/A: {request.ProductName}: <Halal|Haram|Mushbooh>' on a new line, with no additional details, explanations, disclaimers, or markdown. If no data is found, return 'No ingredients available'."
                    : request.ProductName.ToLower().Contains("kitkat")
                        ? $"Provide a list of all ingredients for the product '{request.ProductName}' (a chocolate wafer bar) sold in {request.Country} with their E-code (if applicable), description, and Halal, Haram, or Mushbooh status. Format each entry as 'Ingredient: <name>: <ecode>: <description>: <status>' on a new line, with no additional details, explanations, disclaimers, or markdown. If no ingredients are found, return 'No ingredients available'."
                        : $"Provide a list of all ingredients for the product '{request.ProductName}' sold in {request.Country} with their E-code (if applicable), description, and Halal, Haram, or Mushbooh status. For single-ingredient products like meat, use the product name as the ingredient name. Format each entry as 'Ingredient: <name>: <ecode>: <description>: <status>' on a new line, with no additional details, explanations, disclaimers, or markdown. If no ingredients are found, return 'No ingredients available'."
                : $"Provide a list of all ingredients for the product with barcode '{request.Barcode}' sold in {request.Country} with their E-code (if applicable), description, and Halal, Haram, or Mushbooh status. For single-ingredient products like meat, use the product name as the ingredient name. Format each entry as 'Ingredient: <name>: <ecode>: <description>: <status>' on a new line, with no additional details, explanations, disclaimers, or markdown. If no ingredients are found, return 'No ingredients available'.";

            _logger.LogInformation("Querying Google AI Studio API with: {Query}", query);

            // Prepare the request payload for Gemini API
            var requestPayload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = query }
                        }
                    }
                }
            };

            // Serialize the payload
            string jsonPayload = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // Add API key to the request
            string requestUri = $"{_apiEndpoint}?key={_apiKey}";
            _logger.LogInformation("Sending request to: {RequestUri}", requestUri);

            string? responseBody = null;
            try
            {
                // Send the request
                HttpResponseMessage response = await _httpClient.PostAsync(requestUri, content);
                _logger.LogInformation("Received HTTP status: {StatusCode}", response.StatusCode);
                responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Received response: {ResponseBody}", responseBody);

                // Write response to file for debugging
                File.WriteAllText("gemini_response.txt", responseBody);

                response.EnsureSuccessStatusCode();

                // Parse the response
                var googleResponse = JsonSerializer.Deserialize<GoogleAIResponse>(responseBody);

                if (googleResponse?.candidates == null || googleResponse.candidates.Count == 0)
                {
                    _logger.LogWarning("No valid candidates in Google AI Studio API response.");
                    throw new InvalidOperationException("No valid response from Google AI Studio API.");
                }

                // Extract the text from the first candidate
                string? responseText = googleResponse.candidates[0]?.content?.parts?.FirstOrDefault()?.text;
                if (string.IsNullOrEmpty(responseText))
                {
                    _logger.LogWarning("Empty response from Google AI Studio API for product: {ProductName}", request.ProductName);
                    responseText = "No ingredients available";
                }

                // Write responseText to file for debugging
                File.WriteAllText("response_text.txt", responseText);

                // Parse the response text into ingredients
                var ingredients = ParseIngredients(responseText, request.ProductName, request.Country);

                return new ProductResponse
                {
                    Ingredients = ingredients,
                    Country = request.Country
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request to Google AI Studio API failed with status code: {StatusCode}", ex.StatusCode);
                throw new Exception($"Failed to call Google AI Studio API: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Google AI Studio API response: {ResponseBody}", responseBody ?? "No response body");
                throw new Exception($"Failed to parse Google AI Studio API response: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while processing Google AI Studio API request.");
                throw new Exception($"An error occurred while processing the request: {ex.Message}", ex);
            }
        }

        public async Task<IngredientListResponse> GetIngredientStatusesAsync(IngredientListRequest request)
        {
            if (request == null || request.Ingredients == null || !request.Ingredients.Any())
            {
                _logger.LogWarning("Invalid request: Ingredient list is required.");
                throw new ArgumentException("Ingredient list is required.");
            }

            // Construct the query to enforce a structured format
            string ingredientsList = string.Join(", ", request.Ingredients);
            string query = $"Provide a list of the following ingredients: {ingredientsList}. For each ingredient, include its E-code (if applicable), description, and Halal, Haram, or Mushbooh status. Format each entry as 'Ingredient: <name>: <ecode>: <description>: <status>' on a new line, with no additional details, explanations, disclaimers, or markdown. If no data is available, return 'No data available'.";

            _logger.LogInformation("Querying Google AI Studio API with: {Query}", query);

            // Prepare the request payload for Gemini API
            var requestPayload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = query }
                        }
                    }
                }
            };

            // Serialize the payload
            string jsonPayload = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // Add API key to the request
            string requestUri = $"{_apiEndpoint}?key={_apiKey}";
            _logger.LogInformation("Sending request to: {RequestUri}", requestUri);

            string? responseBody = null;
            try
            {
                // Send the request
                HttpResponseMessage response = await _httpClient.PostAsync(requestUri, content);
                _logger.LogInformation("Received HTTP status: {StatusCode}", response.StatusCode);
                responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Received response: {ResponseBody}", responseBody);

                // Write response to file for debugging
                File.WriteAllText("gemini_response_ingredients.txt", responseBody);

                response.EnsureSuccessStatusCode();

                // Parse the response
                var googleResponse = JsonSerializer.Deserialize<GoogleAIResponse>(responseBody);

                if (googleResponse?.candidates == null || googleResponse.candidates.Count == 0)
                {
                    _logger.LogWarning("No valid candidates in Google AI Studio API response.");
                    throw new InvalidOperationException("No valid response from Google AI Studio API.");
                }

                // Extract the text from the first candidate
                string? responseText = googleResponse.candidates[0]?.content?.parts?.FirstOrDefault()?.text;
                if (string.IsNullOrEmpty(responseText))
                {
                    _logger.LogWarning("Empty response from Google AI Studio API for ingredients: {Ingredients}", ingredientsList);
                    responseText = "No data available";
                }

                // Write responseText to file for debugging
                File.WriteAllText("response_text_ingredients.txt", responseText);

                // Parse the response text into ingredients
                var ingredients = ParseIngredients(responseText, ingredientsList, null);

                return new IngredientListResponse
                {
                    Ingredients = ingredients
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request to Google AI Studio API failed with status code: {StatusCode}", ex.StatusCode);
                throw new Exception($"Failed to call Google AI Studio API: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Google AI Studio API response: {ResponseBody}", responseBody ?? "No response body");
                throw new Exception($"Failed to parse Google AI Studio API response: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while processing Google AI Studio API request.");
                throw new Exception($"An error occurred while processing the request: {ex.Message}", ex);
            }
        }

        private List<Ingredient> ParseIngredients(string responseText, string productName, string country)
        {
            var ingredients = new List<Ingredient>();
            _logger.LogInformation("Parsing response text for product: {ProductName}, country: {Country}", productName, country);

            // Primary regex for structured format with optional "Ingredient:" prefix
            var regex = new Regex(
                @"^(?:Ingredient:\s*)?(?<name>[^:\n]+?)\s*:\s*(?<ecode>[^:\n]*?)\s*:\s*(?<description>[^:\n]*?)\s*:\s*(?<status>Halal|Haram|Mushbooh)$",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var matches = regex.Matches(responseText);

            foreach (Match match in matches)
            {
                if (match.Groups["name"].Success && match.Groups["status"].Success)
                {
                    var ingredientName = match.Groups["name"].Value.Trim();
                    // Skip invalid names
                    if (string.IsNullOrWhiteSpace(ingredientName) ||
                        string.Equals(ingredientName, "Ingredient", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ingredientName, productName, StringComparison.OrdinalIgnoreCase) ||
                        IsInvalidIngredientName(ingredientName))
                    {
                        _logger.LogWarning("Skipping invalid ingredient name: {IngredientName}", ingredientName);
                        continue;
                    }

                    var ecode = match.Groups["ecode"].Value.Trim();
                    var description = match.Groups["description"].Value.Trim();
                    var status = match.Groups["status"].Value.Trim();

                    ingredients.Add(new Ingredient
                    {
                        Id = Guid.NewGuid(),
                        Name = ingredientName,
                        ECode = string.IsNullOrEmpty(ecode) ? "N/A" : ecode,
                        Description = string.IsNullOrEmpty(description) ? ingredientName : description,
                        Status = status,
                        IsHalal = new List<string> { status == "Halal" ? country ?? "None" : "None" },
                        IsHaram = new List<string> { status == "Haram" ? country ?? "None" : "None" },
                        IsMushbooh = new List<string> { status == "Mushbooh" ? country ?? "None" : "None" },
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            // Fallback parsing for less structured responses
            if (ingredients.Count == 0 && !string.IsNullOrWhiteSpace(responseText))
            {
                _logger.LogWarning("Primary regex failed, attempting fallback parsing for product: {ProductName}", productName);
                // Try parsing as a simple list (e.g., "Sugar, Cocoa Butter, Milk Powder")
                var lines = responseText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line) &&
                                   !line.Equals("No ingredients available", StringComparison.OrdinalIgnoreCase));

                foreach (var line in lines)
                {
                    // Try to match the ingredient format in fallback
                    var fallbackMatch = Regex.Match(line,
                        @"^(?:Ingredient:\s*)?(?<name>[^:\n]+?)\s*:\s*(?<ecode>[^:\n]*?)\s*:\s*(?<description>[^:\n]*?)\s*:\s*(?<status>Halal|Haram|Mushbooh)$",
                        RegexOptions.IgnoreCase);

                    if (fallbackMatch.Success)
                    {
                        var ingredientName = fallbackMatch.Groups["name"].Value.Trim();
                        if (string.IsNullOrWhiteSpace(ingredientName) ||
                            string.Equals(ingredientName, "Ingredient", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ingredientName, productName, StringComparison.OrdinalIgnoreCase) ||
                            IsInvalidIngredientName(ingredientName))
                        {
                            _logger.LogWarning("Skipping invalid ingredient in fallback: {IngredientName}", ingredientName);
                            continue;
                        }

                        var ecode = fallbackMatch.Groups["ecode"].Value.Trim();
                        var description = fallbackMatch.Groups["description"].Value.Trim();
                        var status = fallbackMatch.Groups["status"].Value.Trim();

                        ingredients.Add(new Ingredient
                        {
                            Id = Guid.NewGuid(),
                            Name = ingredientName,
                            ECode = string.IsNullOrEmpty(ecode) ? "N/A" : ecode,
                            Description = string.IsNullOrEmpty(description) ? ingredientName : description,
                            Status = status,
                            IsHalal = new List<string> { status == "Halal" ? country ?? "None" : "None" },
                            IsHaram = new List<string> { status == "Haram" ? country ?? "None" : "None" },
                            IsMushbooh = new List<string> { status == "Mushbooh" ? country ?? "None" : "None" },
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        // Skip lines that are clearly not ingredients (e.g., disclaimers, explanations)
                        if (IsInvalidIngredientName(line))
                        {
                            _logger.LogWarning("Skipping non-ingredient line in fallback: {Line}", line);
                            continue;
                        }

                        // Handle simple ingredient names (e.g., "Sugar")
                        var ingredientName = line.Trim();
                        if (string.IsNullOrWhiteSpace(ingredientName) ||
                            string.Equals(ingredientName, "Ingredient", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ingredientName, productName, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning("Skipping invalid ingredient in fallback: {IngredientName}", ingredientName);
                            continue;
                        }

                        ingredients.Add(new Ingredient
                        {
                            Id = Guid.NewGuid(),
                            Name = ingredientName,
                            ECode = "N/A",
                            Description = ingredientName,
                            Status = "Mushbooh", // Default for unknown ingredients
                            IsHalal = new List<string> { "None" },
                            IsHaram = new List<string> { "None" },
                            IsMushbooh = new List<string> { country ?? "None" },
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            // Handle single-ingredient products
            if (ingredients.Count == 0 && IsSingleIngredientProduct(productName))
            {
                _logger.LogInformation("No ingredients parsed, treating {ProductName} as a single-ingredient product", productName);
                string status = DetermineSingleIngredientStatus(productName);
                ingredients.Add(new Ingredient
                {
                    Id = Guid.NewGuid(),
                    Name = productName,
                    Status = status,
                    ECode = "N/A",
                    Description = productName,
                    IsHalal = new List<string> { status == "Halal" ? country ?? "None" : "None" },
                    IsHaram = new List<string> { status == "Haram" ? country ?? "None" : "None" },
                    IsMushbooh = new List<string> { status == "Mushbooh" ? country ?? "None" : "None" },
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else if (ingredients.Count == 0)
            {
                _logger.LogWarning("No valid ingredients parsed for product: {ProductName}. Response text: {ResponseText}", productName, responseText);
            }

            // Remove duplicates by name (case-insensitive)
            var uniqueIngredients = ingredients
                .GroupBy(i => i.Name.ToLower())
                .Select(g => g.First())
                .ToList();

            _logger.LogInformation("Parsed {Count} unique ingredients for product: {ProductName}", uniqueIngredients.Count, productName);
            return uniqueIngredients;
        }

        private bool IsSingleIngredientProduct(string productName)
        {
            if (string.IsNullOrEmpty(productName))
                return false;

            // Simple logic to identify single-ingredient products; expand as needed
            var singleIngredientKeywords = new[] { "pork", "beef", "chicken", "lamb", "fish", "milk", "egg", "honey" };
            return singleIngredientKeywords.Any(keyword => productName.ToLower().Contains(keyword));
        }

        private bool IsMeatProduct(string productName)
        {
            if (string.IsNullOrEmpty(productName))
                return false;

            // Identify meat products for tailored AI query
            var meatKeywords = new[] { "pork", "beef", "chicken", "lamb", "fish", "sausage", "meat" };
            return meatKeywords.Any(keyword => productName.ToLower().Contains(keyword));
        }

        private string DetermineSingleIngredientStatus(string productName)
        {
            // Simple status determination; adjust based on requirements
            if (productName.ToLower().Contains("pork") || productName.ToLower().Contains("pig"))
            {
                return "Haram";
            }
            else if (productName.ToLower().Contains("chicken") || productName.ToLower().Contains("beef") ||
                     productName.ToLower().Contains("lamb") || productName.ToLower().Contains("fish"))
            {
                return "Halal"; // Assuming Halal slaughter; adjust based on country
            }
            else
            {
                return "Mushbooh";
            }
        }

        private bool IsInvalidIngredientName(string name)
        {
            // Check for common patterns in disclaimers or explanatory text
            if (string.IsNullOrWhiteSpace(name))
                return true;

            var lowerName = name.ToLower();
            return lowerName.Contains("since") ||
                   lowerName.Contains("database") ||
                   lowerName.Contains("estimated") ||
                   lowerName.Contains("always check") ||
                   lowerName.Contains("certification") ||
                   lowerName.Contains("important considerations") ||
                   lowerName.Contains("mushbooh means") ||
                   lowerName.Contains("processing methods") ||
                   lowerName.StartsWith("*") ||
                   lowerName.StartsWith("**") ||
                   lowerName.Contains(" and ") || // e.g., "and ideally"
                   lowerName.Length > 50; // Arbitrary length to catch long explanations
        }
    }

    // Define the response model for deserialization
    public class GoogleAIResponse
    {
        public List<Candidate>? candidates { get; set; }
    }

    public class Candidate
    {
        public Content? content { get; set; }
    }

    public class Content
    {
        public List<Part>? parts { get; set; }
    }

    public class Part
    {
        public string? text { get; set; }
    }
}