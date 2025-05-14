using PlantShop.Models;
using System.Security.Cryptography;
using System.Text;

namespace PlantShop.Data
{
    public static class DbSeeder
    {
        public static void Seed(ApplicationDbContext context)
        {
            // Seed Categories
            var categories = new List<Category>
            {
                new Category { Name = "Bonsai", Description = "Cây cảnh bonsai nghệ thuật" },
                new Category { Name = "Cây để bàn", Description = "Cây cảnh mini để bàn làm việc" },
                new Category { Name = "Cây phong thủy", Description = "Cây cảnh phong thủy mang lại may mắn" },
                new Category { Name = "Cây nội thất", Description = "Cây cảnh trang trí nội thất" },
                new Category { Name = "Cây ngoại thất", Description = "Cây cảnh sân vườn, ngoại thất" }
            };
            context.Categories.AddRange(categories);
            context.SaveChanges();

            // Seed Plants từ ảnh trong wwwroot/images
            var imageFiles = new List<string>
            {
                "xuong-rong-tai-chuot.jpg", "xuong-rong-kim-ho.jpg", "xuong-rong-casio.jpg", "web-ban-cay-canh.jpg",
                "xuong-rong-trung-chim.jpg", "xuong-rong-tai-tho.jpg", "xuong-rong-banh-sinh-nhat.jpg", "xuong-rong-kim-ho-thuy-sinh.jpg",
                "xuong-rong-thanh-son.jpg", "xuong-rong-than-long.jpg", "cay-xuong-rong-gymno.jpg", "cay-xuong-rong-bong-gon.jpg",
                "xuong-rong-decor-quan.jpg", "cay-phat-tai-bup-sen.jpg", "cay-trau-ba-de-vuong-thuy-sinh.jpg", "cay-luoi-ho-thuy-sinh.jpg",
                "cay-van-loc-thuy-sinh.jpg", "co-dong-tien.jpg", "cay-phu-quy-thuy-sinh.jpg", "cay-ngoc-ngan-thuy-sinh.jpg",
                "cay-cung-dien-vang-thuy-sinh.jpg", "cay-thuy-tung-thuy-sinh.jpg", "cay-sao-sang-thuy-sinh.jpg", "cay-troc-bac-thuy-sinh.jpg",
                "cay-hanh-phuc-thuy-sinh.jpg", "sen-da-de-vuong-xam.jpg", "cay-sen-da-banh-bao.jpg", "sen-da-sedum-long.jpg",
                "sen-da-bap-cai-tim.jpg", "sen-da-lien-dai-hong.jpg", "cay-sen-da-giva.jpg", "sen-da-cuc-tim.jpg",
                "sen-da-nuda.jpg", "cay-sen-da-kim-cuong-tim.jpg", "chau-sen-da-vat.jpg", "sen-da-casio.jpg",
                "sen-da-canh-buom-bac.jpg", "cay-van-loc.jpg", "cay-ngu-gia-bi.jpg", "cay-thuy-tung.jpg",
                "cay-de-vuong-hoang-kim.jpg", "cay-monstera-albo.jpg", "duoi-cong-van-vang.jpg", "cay-kim-ngan-3-than.jpg",
                "cay-kim-ngan-xoan-ba-than.jpg", "canh-dao-nho.jpg", "cay-nhat-mat-huong.jpg", "cay-phu-quy.jpg",
                "cay-truc-nhat-vang.jpg", "cay-van-nien-thanh.jpg", "cay-trau-ba.jpg", "cay-phat-tai.jpg",
                "cay-ngoc-bich.jpg", "cay-luoi-ho.jpg", "cay-lan-y.jpg", "cay-kim-tien.jpg",
                "cay-kim-ngan.jpg", "cay-duoi-cong.jpg", "cay-cau-tieu-cham.jpg", "cay-hoa-tuong-vi.jpg",
                "cay-hoa-giay.jpg"
            };
            var random = new Random();
            var plantsFromImages = new List<Plant>();
            foreach (var file in imageFiles)
            {
                var name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                    file.Replace(".jpg", "").Replace("-", " ")
                        .Replace("cay ", "Cây ").Replace("xuong rong", "Xương Rồng").Replace("sen da", "Sen Đá")
                        .Replace("thuy sinh", "Thủy Sinh").Replace("kim ngan", "Kim Ngân").Replace("kim tien", "Kim Tiền")
                        .Replace("phat tai", "Phát Tài").Replace("truc nhat", "Trúc Nhật").Replace("trau ba", "Trầu Bà")
                        .Replace("van loc", "Vạn Lộc").Replace("phu quy", "Phú Quý").Replace("ngoc bich", "Ngọc Bích")
                        .Replace("lan y", "Lan Ý").Replace("de vuong", "Đế Vương").Replace("monstera albo", "Monstera Albo")
                        .Replace("duoi cong", "Đuôi Công").Replace("cau tieu cham", "Cau Tiểu Châm")
                        .Replace("hoa tuong vi", "Hoa Tường Vi").Replace("hoa giay", "Hoa Giấy").Trim()
                );
                var desc = $"{name} là cây cảnh trang trí đẹp, phù hợp cho mọi không gian sống và làm việc.";
                var price = random.Next(50000, 500000);
                var stock = random.Next(50, 101);
                plantsFromImages.Add(new Plant
                {
                    Name = name,
                    Description = desc,
                    Price = price,
                    ImageUrl = $"/images/{file}",
                    CategoryId = categories[0].Id,
                    CreatedAt = DateTime.Now,
                    IsActive = true,
                    Stock = stock
                });
            }
            context.Plants.AddRange(plantsFromImages);
            context.SaveChanges();

            // Seed Users (phong phú)
            var users = new List<User>
            {
                new User { Username = "admin", Password = HashPassword("Admin@123"), FullName = "Admin Trưởng", Email = "admin@plantshop.com", PhoneNumber = "0123456789", Address = "123 Admin Street", IsAdmin = true, CreatedAt = DateTime.Now },
                new User { Username = "linh.nguyen", Password = HashPassword("User@123"), FullName = "Nguyễn Thị Linh", Email = "linhnguyen@email.com", PhoneNumber = "0987654321", Address = "25 Trần Hưng Đạo, Hà Nội", IsAdmin = false, CreatedAt = DateTime.Now },
                new User { Username = "tuan.pham", Password = HashPassword("User@123"), FullName = "Phạm Anh Tuấn", Email = "tuanpham@email.com", PhoneNumber = "0912345678", Address = "90 Lý Thường Kiệt, TP.HCM", IsAdmin = false, CreatedAt = DateTime.Now },
                new User { Username = "hoa.le", Password = HashPassword("User@123"), FullName = "Lê Thị Hoa", Email = "hoale@email.com", PhoneNumber = "0933456789", Address = "45 Nguyễn Huệ, Đà Nẵng", IsAdmin = false, CreatedAt = DateTime.Now },
                new User { Username = "minh.tran", Password = HashPassword("User@123"), FullName = "Trần Minh", Email = "minhtran@email.com", PhoneNumber = "0976543210", Address = "78 Pasteur, Huế", IsAdmin = false, CreatedAt = DateTime.Now },
                new User { Username = "anh.nguyen", Password = HashPassword("User@123"), FullName = "Nguyễn Văn Anh", Email = "anhnguyen@email.com", PhoneNumber = "0967123456", Address = "33 Nguyễn Trãi, Nha Trang", IsAdmin = false, CreatedAt = DateTime.Now },
                new User { Username = "huong.tran", Password = HashPassword("User@123"), FullName = "Trần Thu Hương", Email = "huongtran@email.com", PhoneNumber = "0945123789", Address = "60 Hai Bà Trưng, Cần Thơ", IsAdmin = false, CreatedAt = DateTime.Now }
            };
            context.Users.AddRange(users);
            context.SaveChanges();

            var allUsers = context.Users.Where(u => !u.IsAdmin).ToList();
            var allPlants = context.Plants.ToList();
            var orders = new List<Order>();
            var orderDetails = new List<OrderDetail>();

            // Tạo tối thiểu 5 đơn hàng cho mỗi người dùng để có đủ dữ liệu cho PrePost
            foreach (var user in allUsers)
            {
                int userOrderCount = random.Next(70, 100); // Mỗi người dùng có 70-100 đơn hàng
                for (int i = 0; i < userOrderCount; i++)
                {
                    // Lựa chọn cây trước khi tạo đơn hàng để đảm bảo không có đơn hàng trống
                    List<Plant> selectedPlants;

                    // Luôn mua theo pattern dựa trên danh mục để tạo dữ liệu tốt hơn
                    var randomCategory = categories[random.Next(categories.Count)];
                    var plantsInCategory = allPlants.Where(p => p.CategoryId == randomCategory.Id).ToList();

                    if (!plantsInCategory.Any())
                    {
                        // Nếu không có sản phẩm trong danh mục, lấy ngẫu nhiên từ tất cả sản phẩm
                        int itemCount = random.Next(3, 6);
                        selectedPlants = allPlants.OrderBy(x => Guid.NewGuid()).Take(itemCount).ToList();
                    }
                    else
                    {
                        // Số lượng sản phẩm trong đơn hàng
                        int itemCount = Math.Min(random.Next(3, 6), plantsInCategory.Count);
                        selectedPlants = plantsInCategory.OrderBy(x => Guid.NewGuid()).Take(itemCount).ToList();
                    }

                    // Đảm bảo luôn có ít nhất 1 sản phẩm trong đơn hàng
                    if (!selectedPlants.Any())
                    {
                        // Nếu không lấy được sản phẩm nào, bỏ qua đơn hàng này
                        continue;
                    }

                    // Tính tổng tiền trước
                    decimal total = 0;
                    var details = new List<OrderDetail>();

                    foreach (var plant in selectedPlants)
                    {
                        int qty = random.Next(1, 4);
                        total += plant.Price * qty;
                        details.Add(new OrderDetail
                        {
                            PlantId = plant.Id,
                            Quantity = qty,
                            UnitPrice = plant.Price
                        });
                    }

                    // Chỉ tạo đơn hàng nếu tổng tiền > 0
                    if (total > 0)
                    {
                        var order = new Order
                        {
                            CustomerName = user.FullName,
                            PhoneNumber = user.PhoneNumber,
                            Email = user.Email,
                            ShippingAddress = user.Address,
                            OrderDate = DateTime.Now.AddDays(-random.Next(1, 180)), // Thời gian từ 1 đến 180 ngày trước
                            Status = (OrderStatus)random.Next(0, 4), // Luôn là đã giao để đảm bảo đủ dữ liệu cho phân tích
                            TotalAmount = total,
                            UserId = user.Id,
                            Note = ""
                        };

                        context.Orders.Add(order);
                        context.SaveChanges();

                        // Gán OrderId cho các OrderDetail và thêm vào danh sách
                        foreach (var detail in details)
                        {
                            detail.OrderId = order.Id;
                            orderDetails.Add(detail);
                        }
                    }
                }
            }

            context.OrderDetails.AddRange(orderDetails);
            context.SaveChanges();
        }

        private static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }
    }
}
