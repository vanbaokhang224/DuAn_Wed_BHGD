using Web_BHGD.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Web_BHGD
{
    public interface IProductRepository
    {
        Task<IEnumerable<Product>> GetAllAsync();
        Task<Product> GetByIdAsync(int id);
        Task<IEnumerable<Product>> GetByCategoryIdAsync(int id);
        Task AddAsync(Product product);
        Task UpdateAsync(Product product);
        Task DeleteAsync(int id);
        Task<IEnumerable<Product>> GetRelatedProductsAsync(int categoryId, int productId, int take);
        Task<(IEnumerable<Product> Products, int TotalCount)> GetFilteredAndPagedAsync(
            int? categoryId, string searchString, string sortOrder, int page, int pageSize); // Thêm mới
    }
}