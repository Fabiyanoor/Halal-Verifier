using HalalProject.Database.Data;
using HalalProject.Model.Entites;
using Microsoft.EntityFrameworkCore;

namespace Halal_project_BL.Repositories
{
    public interface IAuthRepository
    {
        Task<UserModel> GetUserByLogin(string username, string password);
        Task<UserModel> GetUserByUsername(string username);
        Task<UserModel> GetUserByEmail(string email);
        Task AddUser(UserModel user);
    }

    public class AuthRepository : IAuthRepository
    {
        private readonly AppDbContext _dbContext;

        public AuthRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<UserModel?> GetUserByLogin(string email, string password)
        {
            return _dbContext.Users
                .Include(n => n.UserRoles)
                .ThenInclude(n => n.Role)
                .FirstOrDefaultAsync(n => n.Email == email);
        }

        public Task<UserModel?> GetUserByUsername(string username)
        {
            return _dbContext.Users
                .Include(n => n.UserRoles)
                .ThenInclude(n => n.Role)
                .FirstOrDefaultAsync(n => n.Username == username);
        }

        public Task<UserModel?> GetUserByEmail(string email)
        {
            return _dbContext.Users
                .Include(n => n.UserRoles)
                .ThenInclude(n => n.Role)
                .FirstOrDefaultAsync(n => n.Email == email);
        }

        public async Task AddUser(UserModel user)
        {
            await _dbContext.Users.AddAsync(user);
            await _dbContext.SaveChangesAsync();
        }
    }
}