using Authentication;
using HalalProject.Model.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Localization;
using Newtonsoft.Json;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Halal_Project.Web
{
    public class ApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ProtectedLocalStorage _localStorage;
        private readonly NavigationManager _navigationManager;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly int _timeoutSeconds = 30;
        private readonly string _baseUrl;
        private string? _cachedToken;

        public ApiClient(HttpClient httpClient,
                        IConfiguration configuration,
                        ProtectedLocalStorage localStorage,
                        NavigationManager navigationManager,
                        AuthenticationStateProvider authStateProvider)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _navigationManager = navigationManager;
            _authStateProvider = authStateProvider;
            _baseUrl = configuration["ApiBaseUrl"] ?? navigationManager.BaseUri;
            _httpClient.Timeout = TimeSpan.FromSeconds(_timeoutSeconds);
        }

        public async Task SetAuthorizeHeader()
        {
            try
            {
                var sessionState = (await _localStorage.GetAsync<LoginResponseModel>("sessionState")).Value;
                if (sessionState != null && !string.IsNullOrEmpty(sessionState.Token))
                {
                    if (sessionState.TokenExpired < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    {
                        await ((CustomAuthStateProvider)_authStateProvider).MarkUserAsLoggedOut();
                        _navigationManager.NavigateTo("/login");
                    }
                    else if (sessionState.TokenExpired < DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds())
                    {
                        var res = await _httpClient.GetFromJsonAsync<LoginResponseModel>($"/api/auth/loginByRefeshToken?refreshToken={sessionState.RefreshToken}");
                        if (res != null)
                        {
                            await ((CustomAuthStateProvider)_authStateProvider).MarkUserAsAuthenticated(res);
                            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", res.Token);
                        }
                        else
                        {
                            await ((CustomAuthStateProvider)_authStateProvider).MarkUserAsLoggedOut();
                            _navigationManager.NavigateTo("/login");
                        }
                    }
                    else
                    {
                        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionState.Token);
                    }

                    var requestCulture = new RequestCulture(
                            CultureInfo.CurrentCulture,
                            CultureInfo.CurrentUICulture
                        );
                    var cultureCookieValue = CookieRequestCultureProvider.MakeCookieValue(requestCulture);

                    _httpClient.DefaultRequestHeaders.Add("Cookie", $"{CookieRequestCultureProvider.DefaultCookieName}={cultureCookieValue}");
                }
            }
            catch (Exception ex)
            {
            }
        }
        public async Task<T1> PostAsync<T1, T2>(string path, T2 postModel)
        {

            var res = await _httpClient.PostAsJsonAsync(path, postModel);
            if (res != null && res.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<T1>(await res.Content.ReadAsStringAsync());
            }
            return default;
        }
        private async Task LogoutAndRedirect()
        {
            _cachedToken = null;
            await ((CustomAuthStateProvider)_authStateProvider).MarkUserAsLoggedOut();
            _navigationManager.NavigateTo("/login", true);
        }

        public string GetImageUrl(string relativeUrl)
        {
            if (string.IsNullOrEmpty(relativeUrl))
                return "/Assets/placeholder.jpg";

            // Ensure the URL starts with a slash
            if (!relativeUrl.StartsWith("/"))
                relativeUrl = "/" + relativeUrl;

            return relativeUrl;
        }
        public async Task<T1> PutAsync<T1, T2>(string path, T2 postModel)
        {
            await SetAuthorizeHeader();
            var response = await _httpClient.PutAsJsonAsync(path, postModel);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<T1>();
            }
            throw new HttpRequestException($"API request failed with status {response.StatusCode}");
        }

        public async Task<T> GetFromJsonAsync<T>(string path)
        {
            await SetAuthorizeHeader();
            return await _httpClient.GetFromJsonAsync<T>(path);
        }

        public async Task<TResponse> PostAsJsonAsync<TResponse, TRequest>(string path, TRequest request)
        {
            await SetAuthorizeHeader();
            var response = await _httpClient.PostAsJsonAsync(path, request);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TResponse>(content);
        }
        // In ApiClient.cs
        public async Task<T> PostAsync<T>(string path, Func<MultipartFormDataContent> contentFactory)
        {
            await SetAuthorizeHeader();
            try
            {
                using var content = contentFactory();
                var response = await _httpClient.PostAsync(path, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"API request failed: {responseContent}");
                }

                return JsonConvert.DeserializeObject<T>(responseContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PostAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<T> PutAsync<T>(string path, Func<MultipartFormDataContent> contentFactory)
        {
            await SetAuthorizeHeader();
            try
            {
                using var content = contentFactory();
                var response = await _httpClient.PutAsync(path, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"API request failed: {responseContent}");
                }

                return JsonConvert.DeserializeObject<T>(responseContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PutAsync: {ex.Message}");
                throw;
            }
        }
        public async Task<T> DeleteAsync<T>(string path)
        {
            await SetAuthorizeHeader();
            try
            {
                var response = await _httpClient.DeleteAsync(path);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"API request failed: {responseContent}");
                }

                return JsonConvert.DeserializeObject<T>(responseContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeleteAsync: {ex.Message}");
                throw;
            }
        }


    }
}
