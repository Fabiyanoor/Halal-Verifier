using HalalProject.Database.Data;
using HalalProject.Model.Entites;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Halal_project_BL.Repositories
{
    public interface IProductRequestRepository
    {
        Task CreateRequest(ProductChangeRequest request);
        Task UpdateRequest(ProductChangeRequest request);
        Task<List<ProductChangeRequest>> GetAllRequests();
            Task DeleteRequest(Guid requestId);
        Task<List<ProductChangeRequest>> GetRequestsByStatus(string status);
        Task<List<ProductChangeRequest>> GetRequestsByUser(string userId);
    }

    public class ProductRequestRepository : IProductRequestRepository
    {
        private readonly AppDbContext _dbContext;

        public ProductRequestRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task CreateRequest(ProductChangeRequest request)
        {
            await _dbContext.ProductChangeRequests.AddAsync(request);
            await _dbContext.SaveChangesAsync();
        }

        public async Task UpdateRequest(ProductChangeRequest request)
        {
            var existingRequest = await _dbContext.ProductChangeRequests
                .FirstOrDefaultAsync(r => r.RequestId == request.RequestId);

            if (existingRequest != null)
            {
                _dbContext.Entry(existingRequest).CurrentValues.SetValues(request);
                await _dbContext.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"Request with ID {request.RequestId} not found");
            }
        }

        public async Task DeleteRequest(Guid requestId)
        {
            var request = await _dbContext.ProductChangeRequests
                .FirstOrDefaultAsync(r => r.RequestId == requestId);

            if (request != null)
            {
                _dbContext.ProductChangeRequests.Remove(request);
                await _dbContext.SaveChangesAsync();
            }
        }

     
        public async Task<List<ProductChangeRequest>> GetAllRequests()
        {
            return await _dbContext.ProductChangeRequests
                .Include(r => r.OriginalProduct)
                .ToListAsync();
        }
        public async Task<List<ProductChangeRequest>> GetRequestsByStatus(string status)
        {
            return await _dbContext.ProductChangeRequests
                .Where(r => r.RequestStatus == status)
                .Include(r => r.OriginalProduct)
                .ToListAsync();
        }

        public async Task<List<ProductChangeRequest>> GetRequestsByUser(string userId)
        {
            return await _dbContext.ProductChangeRequests
                .Where(r => r.RequestedBy == userId)
                .Include(r => r.OriginalProduct)
                .ToListAsync();
        }
    }
}