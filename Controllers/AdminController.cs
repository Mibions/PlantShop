using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantShop.Data;
using PlantShop.Models;

namespace PlantShop.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 10;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Kiểm tra quyền admin
        private bool IsAdmin()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return false;

            var user = _context.Users.Find(userId);
            return user?.IsAdmin == true;
        }

        // Trang chủ admin
        public IActionResult Index()
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            var stats = new
            {
                TotalProducts = _context.Plants.Count(),
                TotalCategories = _context.Categories.Count(),
                TotalOrders = _context.Orders.Count(),
                TotalUsers = _context.Users.Count(),
                RecentOrders = _context.Orders
                    .Include(o => o.User)
                    .OrderByDescending(o => o.OrderDate)
                    .Take(5)
                    .ToList()
            };

            return View(stats);
        }

        #region Plants Management
        public async Task<IActionResult> Plants(int page = 1)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            var plants = await _context.Plants
                .Include(p => p.Category)
                .OrderBy(p => p.Id)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var totalPlants = await _context.Plants.CountAsync();
            var totalPages = (int)Math.Ceiling(totalPlants / (double)PageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            ViewBag.HasPreviousPage = page > 1;
            ViewBag.HasNextPage = page < totalPages;

            ViewBag.Categories = await _context.Categories.ToListAsync();

            return View(plants);
        }

        public async Task<IActionResult> CreatePlant()
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View("CreateProduct");
        }

        [HttpPost]
        public async Task<IActionResult> CreatePlant(Plant plant)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            if (ModelState.IsValid)
            {
                plant.IsActive = true;
                _context.Add(plant);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm sản phẩm thành công!";
                return RedirectToAction(nameof(Plants));
            }
            return RedirectToAction(nameof(Plants));
        }

        public async Task<IActionResult> EditPlant(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            // Debug: In thông tin id
            Console.WriteLine($"EditPlant GET được gọi: Id={id}");

            var plant = await _context.Plants.FindAsync(id);
            if (plant == null)
            {
                return NotFound();
            }

            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View("EditProduct", plant);
        }

        [HttpPost]
        public async Task<IActionResult> EditPlant(Plant plant)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            // Debug: In thông tin plant
            Console.WriteLine($"EditPlant được gọi: Id={plant.Id}, Name={plant.Name}, Price={plant.Price}, Stock={plant.Stock}, IsActive={plant.IsActive}, CategoryId={plant.CategoryId}");

            try
            {
                if (ModelState.IsValid)
                {
                    // Debug form data
                    foreach (var key in Request.Form.Keys)
                    {
                        Console.WriteLine($"Form data: {key} = {Request.Form[key]}");
                    }

                    try
                    {
                        _context.Update(plant);
                        await _context.SaveChangesAsync();
                        TempData["Success"] = "Cập nhật sản phẩm thành công!";
                        return RedirectToAction(nameof(Plants));
                    }
                    catch (Exception ex)
                    {
                        // Debug: In lỗi chi tiết
                        Console.WriteLine($"Lỗi khi cập nhật sản phẩm: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        }
                        ModelState.AddModelError("", $"Lỗi khi cập nhật sản phẩm: {ex.Message}");
                    }
                }
                else
                {
                    // Debug: In lỗi ModelState
                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        Console.WriteLine($"Lỗi ModelState: {error.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi không xác định: {ex.Message}");
            }

            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View("EditProduct", plant);
        }

        [HttpPost]
        public async Task<IActionResult> DeletePlant(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            // Debug: In thông tin id và dữ liệu form
            Console.WriteLine($"DeletePlant được gọi: Id={id}");
            foreach (var key in Request.Form.Keys)
            {
                Console.WriteLine($"Form data: {key} = {Request.Form[key]}");
            }

            try
            {
                var plant = await _context.Plants.FindAsync(id);
                if (plant != null)
                {
                    Console.WriteLine($"Tìm thấy sản phẩm: Id={plant.Id}, Name={plant.Name}, IsActive={plant.IsActive}");
                    plant.IsActive = false;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Xóa sản phẩm thành công!";
                }
                else
                {
                    Console.WriteLine($"Không tìm thấy sản phẩm với Id={id}");
                }
            }
            catch (Exception ex)
            {
                // Debug: In lỗi chi tiết
                Console.WriteLine($"Lỗi khi xóa sản phẩm: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                TempData["Error"] = $"Lỗi khi xóa sản phẩm: {ex.Message}";
            }

            return RedirectToAction(nameof(Plants));
        }

        private bool PlantExists(int id)
        {
            return _context.Plants.Any(e => e.Id == id);
        }
        #endregion

        #region Categories Management
        public async Task<IActionResult> Categories()
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            return View(await _context.Categories.ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> CreateCategory(Category category)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            if (ModelState.IsValid)
            {
                _context.Add(category);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm danh mục thành công!";
                return RedirectToAction(nameof(Categories));
            }
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        public async Task<IActionResult> EditCategory(Category category)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(category);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật danh mục thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoryExists(category.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Categories));
            }
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa danh mục thành công!";
            }
            return RedirectToAction(nameof(Categories));
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.Id == id);
        }
        #endregion

        #region Orders Management
        public async Task<IActionResult> Orders(int page = 1)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            var orders = await _context.Orders
                .Include(o => o.User)
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var totalOrders = await _context.Orders.CountAsync();
            var totalPages = (int)Math.Ceiling(totalOrders / (double)PageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            ViewBag.HasPreviousPage = page > 1;
            ViewBag.HasNextPage = page < totalPages;

            return View(orders);
        }

        public async Task<IActionResult> OrderDetails(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Plant)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int id, OrderStatus status)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            var order = await _context.Orders.FindAsync(id);
            if (order != null)
            {
                order.Status = status;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cập nhật trạng thái đơn hàng thành công!";
            }
            return RedirectToAction(nameof(Orders));
        }
        #endregion

        #region Users Management
        public async Task<IActionResult> Users(int page = 1)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            var users = await _context.Users
                .OrderBy(u => u.Id)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var totalUsers = await _context.Users.CountAsync();
            var totalPages = (int)Math.Ceiling(totalUsers / (double)PageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            ViewBag.HasPreviousPage = page > 1;
            ViewBag.HasNextPage = page < totalPages;

            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cập nhật trạng thái người dùng thành công!";
            }
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAdmin(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.IsAdmin = !user.IsAdmin;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cập nhật quyền admin thành công!";
            }
            return RedirectToAction(nameof(Users));
        }

        public async Task<IActionResult> UserDetails(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }
        #endregion
    }
}