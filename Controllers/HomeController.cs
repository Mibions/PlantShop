using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantShop.Data;
using PlantShop.Models;
using PlantShop.Services;
using System.Text.Json;

namespace PlantShop.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly PythonRecommenderService _recommenderService;
    private const int PageSize = 9;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, PythonRecommenderService recommenderService)
    {
        _logger = logger;
        _context = context;
        _recommenderService = recommenderService;
    }

    public async Task<IActionResult> Index(int page = 1, int? categoryId = null)
    {
        var categories = await _context.Categories.ToListAsync();
        ViewBag.Categories = categories;
        ViewBag.SelectedCategoryId = categoryId;

        var query = _context.Plants.Include(p => p.Category).Where(p => p.IsActive);
        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }
        var totalPlants = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalPlants / (double)PageSize);
        page = Math.Max(1, Math.Min(page, totalPages));
        var plants = await query.OrderBy(p => p.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.HasPreviousPage = page > 1;
        ViewBag.HasNextPage = page < totalPages;

        // Gợi ý sản phẩm cho user nếu đã đăng nhập
        int? userId = HttpContext.Session.GetInt32("UserId");
        if (userId != null)
        {
            try
            {
                _logger.LogInformation("Đang lấy gợi ý sản phẩm cho người dùng ID: {UserId}", userId);

                // Lấy danh sách các sản phẩm mà người dùng đã từng mua từ các đơn hàng không bị hủy
                var basketItems = await _context.Orders
                    .Where(o => o.UserId == userId && o.Status != OrderStatus.Cancelled)
                    .SelectMany(o => o.OrderDetails)
                    .Select(od => od.PlantId)
                    .Distinct()
                    .ToListAsync();

                List<Plant> recommendedPlants;

                // Nếu người dùng đã mua sản phẩm, sử dụng recommender service
                if (basketItems.Any())
                {
                    _logger.LogInformation("Người dùng đã mua {Count} sản phẩm", basketItems.Count);
                    recommendedPlants = await _recommenderService.GetRecommendations(basketItems, userId, 6);
                }
                // Nếu người dùng chưa mua sản phẩm nào, lấy sản phẩm phổ biến
                else
                {
                    _logger.LogInformation("Người dùng chưa mua sản phẩm nào, lấy sản phẩm phổ biến");

                    // Lấy top 6 sản phẩm phổ biến dựa trên số lượng đơn hàng
                    var popularProductIds = await _context.OrderDetails
                        .GroupBy(od => od.PlantId)
                        .Select(g => new { PlantId = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .Take(6)
                        .Select(x => x.PlantId)
                        .ToListAsync();

                    if (popularProductIds.Any())
                    {
                        recommendedPlants = await _context.Plants
                            .Where(p => popularProductIds.Contains(p.Id) && p.IsActive)
                            .ToListAsync();

                        _logger.LogInformation("Đã lấy {Count} sản phẩm phổ biến", recommendedPlants.Count);
                    }
                    else
                    {
                        // Nếu không có sản phẩm phổ biến, lấy ngẫu nhiên 6 sản phẩm
                        _logger.LogInformation("Không có sản phẩm phổ biến, lấy ngẫu nhiên");
                        recommendedPlants = await _context.Plants
                            .Where(p => p.IsActive)
                            .OrderBy(p => Guid.NewGuid()) // Sắp xếp ngẫu nhiên
                            .Take(6)
                            .ToListAsync();
                    }
                }

                if (recommendedPlants.Any())
                {
                    _logger.LogInformation("Đã tìm thấy {Count} sản phẩm gợi ý: {Products}",
                        recommendedPlants.Count,
                        string.Join(", ", recommendedPlants.Select(p => p.Name)));
                    ViewBag.RecommendedProducts = recommendedPlants;
                }
                else
                {
                    _logger.LogWarning("Không nhận được sản phẩm gợi ý, lấy ngẫu nhiên");
                    // Lấy 6 sản phẩm mới nhất làm sản phẩm gợi ý
                    var latestProducts = await _context.Plants
                        .Where(p => p.IsActive)
                        .OrderByDescending(p => p.Id)
                        .Take(6)
                        .ToListAsync();
                    ViewBag.RecommendedProducts = latestProducts;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy gợi ý sản phẩm: {Message}", ex.Message);
                // Trường hợp lỗi, trả về danh sách trống
                ViewBag.RecommendedProducts = new List<Plant>();
            }
        }
        return View(plants);
    }

    public async Task<IActionResult> Details(int id)
    {
        var plant = await _context.Plants
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (plant == null)
        {
            return NotFound();
        }

        // Gợi ý sản phẩm dựa trên sản phẩm đang xem
        try
        {
            _logger.LogInformation("Đang lấy gợi ý sản phẩm cho PlantID: {PlantId}", id);

            var recommendedPlants = await _recommenderService.GetRecommendations(new List<int> { id }, null, 6);

            if (recommendedPlants.Any())
            {
                _logger.LogInformation("Đã tìm thấy {Count} sản phẩm gợi ý: {Products}",
                    recommendedPlants.Count,
                    string.Join(", ", recommendedPlants.Select(p => p.Name)));
            }
            else
            {
                _logger.LogWarning("Không nhận được sản phẩm gợi ý nào");
            }

            ViewBag.RecommendedPlants = recommendedPlants;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy gợi ý sản phẩm: {Message}", ex.Message);
        }

        return View(plant);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
