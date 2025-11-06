using System.Threading.Tasks;
using Halal_project_BL.Repositories;
using HalalProject.Model.Entites;

namespace Halal_project_BL.Services
{
    public interface IAuthService
    {
        Task<UserModel> GetUserByLogin(string username, string password);
        Task<UserModel> GetUserByUsername(string username);
        Task<UserModel> GetUserByEmail(string email);
        Task AddUser(UserModel user);
    }

    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _authRepository;

        public AuthService(IAuthRepository authRepository)
        {
            _authRepository = authRepository;
        }

        public Task<UserModel> GetUserByLogin(string email, string password)
        {
            return _authRepository.GetUserByLogin(email, password);
        }

        public Task<UserModel> GetUserByUsername(string username)
        {
            return _authRepository.GetUserByUsername(username);
        }

        public Task<UserModel> GetUserByEmail(string email)
        {
            return _authRepository.GetUserByEmail(email);
        }

        public Task AddUser(UserModel user)
        {
            return _authRepository.AddUser(user);
        }
    }
}