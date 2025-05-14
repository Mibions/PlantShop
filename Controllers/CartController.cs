using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantShop.Data;
using PlantShop.Models;
using System.Text.Json;

namespace PlantShop.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        private List<CartItem> GetCartFromSession()
        {
            var cartJson = HttpContext.Session.GetString("Cart");
            if (string.IsNullOrEmpty(cartJson))
            {
                return new List<CartItem>();
            }
            return JsonSerializer.Deserialize<List<CartItem>>(cartJson) ?? new List<CartItem>();
        }

        private void SaveCartToSession(List<CartItem> cart)
        {
            var cartJson = JsonSerializer.Serialize(cart);
            HttpContext.Session.SetString("Cart", cartJson);
        }

        [HttpPost]
        public IActionResult AddToCart(int plantId, int quantity)
        {
            try
            {
                Console.WriteLine($"AddToCart được gọi: plantId={plantId}, quantity={quantity}");

                if (quantity <= 0)
                {
                    quantity = 1; // Đảm bảo số lượng tối thiểu là 1
                }

                var plant = _context.Plants.Find(plantId);
                if (plant == null)
                {
                    Console.WriteLine($"Không tìm thấy sản phẩm với ID: {plantId}");
                    return NotFound($"Không tìm thấy sản phẩm với ID: {plantId}");
                }

                // Kiểm tra tồn kho
                if (quantity > plant.Stock)
                {
                    Console.WriteLine($"Số lượng ({quantity}) vượt quá tồn kho ({plant.Stock})");
                    return BadRequest($"Số lượng vượt quá tồn kho. Hiện chỉ còn {plant.Stock} sản phẩm.");
                }

                var cart = GetCartFromSession();
                var cartItem = cart.FirstOrDefault(i => i.PlantId == plantId);

                if (cartItem != null)
                {
                    cartItem.Quantity += quantity;
                    Console.WriteLine($"Cập nhật số lượng: {cartItem.PlantName}, mới: {cartItem.Quantity}");
                }
                else
                {
                    cart.Add(new CartItem
                    {
                        PlantId = plant.Id,
                        PlantName = plant.Name,
                        Price = plant.Price,
                        Quantity = quantity,
                        ImageUrl = plant.ImageUrl
                    });
                    Console.WriteLine($"Thêm mới: {plant.Name}, số lượng: {quantity}");
                }

                SaveCartToSession(cart);
                Console.WriteLine($"Đã lưu giỏ hàng: {cart.Count} mục, tổng: {cart.Sum(i => i.Quantity)}");
                return Json(new { success = true, count = cart.Sum(i => i.Quantity) });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi thêm vào giỏ hàng: {ex.Message}");
                return StatusCode(500, $"Lỗi thêm vào giỏ hàng: {ex.Message}");
            }
        }

        [HttpPost]
        public IActionResult UpdateQuantity(int plantId, int quantity)
        {
            if (quantity < 1)
            {
                return BadRequest("Số lượng phải lớn hơn 0");
            }

            var cart = GetCartFromSession();
            var cartItem = cart.FirstOrDefault(i => i.PlantId == plantId);

            if (cartItem != null)
            {
                cartItem.Quantity = quantity;
                SaveCartToSession(cart);
            }

            return Json(new
            {
                success = true,
                total = cart.Sum(i => i.Total),
                count = cart.Sum(i => i.Quantity),
                itemTotal = cartItem != null ? cartItem.Total : 0
            });
        }

        [HttpPost]
        public IActionResult RemoveFromCart(int plantId)
        {
            var cart = GetCartFromSession();
            cart.RemoveAll(i => i.PlantId == plantId);
            SaveCartToSession(cart);
            return Json(new
            {
                success = true,
                total = cart.Sum(i => i.Total),
                count = cart.Sum(i => i.Quantity)
            });
        }

        public IActionResult GetCartCount()
        {
            var cart = GetCartFromSession();
            return Content(cart.Sum(i => i.Quantity).ToString());
        }

        public IActionResult Index()
        {
            var cart = GetCartFromSession();
            return View(cart);
        }

        public IActionResult Checkout()
        {
            var cart = GetCartFromSession();
            if (!cart.Any())
            {
                return RedirectToAction("Index");
            }
            return RedirectToAction("Checkout", "Order");
        }
    }

    public class CartItem
    {
        public int PlantId { get; set; }
        public string PlantName { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; }
        public decimal Total => Price * Quantity;
    }
}