using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantShop.Data;
using PlantShop.Models;
using PlantShop.Services;
using System.Text.Json;
using System.Linq;

namespace PlantShop.Controllers
{
    public class RecommendationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PythonRecommenderService _recommenderService;

        public RecommendationController(ApplicationDbContext context, PythonRecommenderService recommenderService)
        {
            _context = context;
            _recommenderService = recommenderService;
        }

        [HttpGet]
        public async Task<IActionResult> GetRecommendations()
        {
            try
            {
                // Lấy thông tin người dùng đăng nhập từ session
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return BadRequest("Người dùng chưa đăng nhập");
                }
                else
                {
                    Console.WriteLine($"UserId: {userId}");
                }

                // Lấy danh sách các đơn hàng chưa bị hủy của người dùng
                var orders = await _context.Orders
                    .Where(o => o.UserId == userId && o.Status != OrderStatus.Cancelled)
                    .Include(o => o.OrderDetails)  // Bao gồm OrderDetails để lấy sản phẩm
                    .ThenInclude(od => od.Plant)   // Bao gồm Plant để lấy thông tin sản phẩm
                    .ToListAsync();

                if (orders.Any())
                {
                    Console.WriteLine($"Lấy được {orders.Count} đơn hàng từ database.");
                }
                else
                {
                    Console.WriteLine("Không có đơn hàng nào.");
                }

                // Lấy danh sách các sản phẩm mà người dùng đã từng mua
                var basketItems = orders
                    .SelectMany(o => o.OrderDetails)
                    .Select(od => od.PlantId)
                    .Distinct()
                    .ToList();

                if (basketItems.Any())
                {
                    Console.WriteLine($"Người dùng đã mua {basketItems.Count} sản phẩm.");
                }
                else
                {
                    Console.WriteLine("Người dùng chưa mua sản phẩm nào.");
                    return Ok(new List<object>()); // Trả về danh sách rỗng nếu chưa mua gì
                }

                // Gọi dịch vụ gợi ý với danh sách các sản phẩm đã mua
                var recommendations = await _recommenderService.GetRecommendations(basketItems, userId);

                if (recommendations.Any())
                {
                    Console.WriteLine($"Đã nhận được {recommendations.Count} gợi ý sản phẩm.");
                }
                else
                {
                    Console.WriteLine("Không có gợi ý sản phẩm nào.");
                }

                // Chuyển đổi danh sách gợi ý thành viewModel để trả về JSON
                var viewModels = recommendations.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    price = p.Price,
                    imageUrl = p.ImageUrl,
                    category = p.Category?.Name,
                    stock = p.Stock
                }).ToList();

                return Json(viewModels);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi: {ex.Message}");
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }
    }
}
