using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantShop.Data;
using PlantShop.Models;
using System.Text.Json;
using PlantShop.DTOs;

namespace PlantShop.Controllers
{
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const string CartSessionKey = "Cart";

        public OrderController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Checkout()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Checkout", "Order") });
            }

            var cart = GetCart();
            if (!cart.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            var user = _context.Users.Find(userId.Value);
            var dto = new OrderCreateDto();
            if (user != null)
            {
                dto.CustomerName = user.FullName;
                dto.PhoneNumber = user.PhoneNumber;
                dto.Email = user.Email;
                dto.ShippingAddress = user.Address;
            }
            ViewBag.Cart = cart;
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(OrderCreateDto dto)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Checkout", "Order") });
            }

            var cart = GetCart();
            if (!cart.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Kiểm tra số lượng tồn kho
                    foreach (var item in cart)
                    {
                        var plant = await _context.Plants.FindAsync(item.PlantId);
                        if (plant == null)
                        {
                            ModelState.AddModelError("", $"Sản phẩm không tồn tại: {item.PlantName}");
                            ViewBag.Cart = cart;
                            return View(dto);
                        }
                        if (plant.Stock < item.Quantity)
                        {
                            ModelState.AddModelError("", $"Số lượng sản phẩm {item.PlantName} trong kho không đủ");
                            ViewBag.Cart = cart;
                            return View(dto);
                        }
                    }

                    // Bắt đầu transaction
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        // Tạo Order từ DTO
                        var order = new Order
                        {
                            CustomerName = dto.CustomerName,
                            PhoneNumber = dto.PhoneNumber,
                            Email = dto.Email,
                            ShippingAddress = dto.ShippingAddress,
                            Note = dto.Note,
                            OrderDate = DateTime.Now,
                            TotalAmount = cart.Sum(item => item.Total),
                            Status = OrderStatus.Pending,
                            UserId = userId
                        };
                        _context.Orders.Add(order);
                        await _context.SaveChangesAsync();

                        // Tạo và lưu order details
                        var orderDetails = new List<OrderDetail>();
                        foreach (var item in cart)
                        {
                            var plant = await _context.Plants.FindAsync(item.PlantId);
                            if (plant == null)
                            {
                                throw new Exception($"Không tìm thấy sản phẩm với ID: {item.PlantId}");
                            }

                            var orderDetail = new OrderDetail
                            {
                                OrderId = order.Id,
                                PlantId = item.PlantId,
                                Quantity = item.Quantity,
                                UnitPrice = item.Price
                            };
                            orderDetails.Add(orderDetail);

                            // Cập nhật số lượng tồn kho
                            plant.Stock -= item.Quantity;
                            if (plant.Stock < 0)
                            {
                                throw new Exception($"Số lượng tồn kho không đủ cho sản phẩm: {plant.Name}");
                            }
                        }

                        // Lưu tất cả order details
                        _context.OrderDetails.AddRange(orderDetails);
                        await _context.SaveChangesAsync();

                        // Commit transaction
                        await transaction.CommitAsync();

                        // Xóa giỏ hàng
                        ClearCart();

                        // Chuyển hướng đến trang xác nhận
                        TempData["SuccessMessage"] = "Đặt hàng thành công! Cảm ơn bạn đã mua hàng.";
                        return RedirectToAction(nameof(Confirmation), new { id = order.Id });
                    }
                    catch (Exception ex)
                    {
                        // Rollback transaction nếu có lỗi
                        await transaction.RollbackAsync();
                        throw new Exception($"Lỗi khi xử lý đơn hàng: {ex.Message}", ex);
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Có lỗi xảy ra khi xử lý đơn hàng. Vui lòng thử lại sau.");
                    // Log lỗi ở đây
                    Console.WriteLine($"Lỗi đặt hàng: {ex.Message}");
                }
            }

            ViewBag.Cart = cart;
            return View(dto);
        }

        public async Task<IActionResult> Confirmation(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Plant)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            ViewBag.SuccessMessage = TempData["SuccessMessage"];
            return View(order);
        }

        private List<CartItem> GetCart()
        {
            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            return cartJson == null ? new List<CartItem>() : JsonSerializer.Deserialize<List<CartItem>>(cartJson);
        }

        private void ClearCart()
        {
            HttpContext.Session.Remove(CartSessionKey);
        }
    }
}