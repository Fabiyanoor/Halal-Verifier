using Halal_project_BL.Services;
using HalalProject.Database.Data;
using HalalProject.Model.Entites;
using HalalProject.Model.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(IConfiguration configuration, IAuthService authService, DbContext dbContext) : ControllerBase
    {
        private readonly AppDbContext _dbContext ;

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseModel>> Login([FromBody] LoginModel loginModel)
        {
            var user = await authService.GetUserByLogin(loginModel.Email, loginModel.Password);
            if (user != null)
            {
                var token = GenerateJwtToken(user);
                return Ok(new LoginResponseModel
                {
                    Token = token
                });
            }
            return Unauthorized("Invalid email or password.");
        }

        [HttpPost("register")]
        public async Task<ActionResult> Register([FromBody] RegisterModel registerModel)
        {
            // Check if username exists
            if (await authService.GetUserByUsername(registerModel.Username) != null)
            {
                return BadRequest("Username already exists");
            }

            // Get the existing "User" role (MUST exist in DB)
            var userRole = await _dbContext.Roles.FirstAsync(r => r.RoleName == "User");

            var user = new UserModel
            {
                Username = registerModel.Username,
                Email = registerModel.Email,
                Password = registerModel.Password, // Remember to hash this!
                UserRoles = new List<UserRoleModel>
        {
            new UserRoleModel
            {
                ID = userRole.ID // Assign existing role by ID
            }
        }
            };

            await authService.AddUser(user);
            return Ok("User registered successfully");
        }

        private string GenerateJwtToken(UserModel user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
            };
            claims.AddRange(user.UserRoles.Select(n => new Claim(ClaimTypes.Role, n.Role.RoleName)));

            string secret = configuration.GetValue<string>("Jwt:Secret");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "MyAwesomeApp",
                audience: "MyAwesomeAudience",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: creds
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}