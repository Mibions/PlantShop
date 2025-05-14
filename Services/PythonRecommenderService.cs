using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using PlantShop.Data;
using PlantShop.Models;
using System.Diagnostics;
using System.Text;

namespace PlantShop.Services
{
    public class PythonRecommenderService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<PythonRecommenderService> _logger;

        public PythonRecommenderService(ApplicationDbContext context, IWebHostEnvironment environment, ILogger<PythonRecommenderService> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        private List<TransactionData> GetTransactionData(int? userId)
        {
            try
            {
                List<TransactionData> transactions = new List<TransactionData>();

                // Nếu có userId, ưu tiên lấy tất cả đơn hàng của người dùng đó
                if (userId.HasValue)
                {
                    _logger.LogInformation("Lấy đơn hàng của người dùng ID: {UserId}", userId.Value);

                    var userOrders = _context.Orders
                        .Where(o => o.UserId == userId.Value && o.Status != OrderStatus.Cancelled)
                        .Include(o => o.OrderDetails)
                        .ToList();

                    _logger.LogInformation("Đã tìm thấy {Count} đơn hàng của người dùng", userOrders.Count);

                    foreach (var order in userOrders)
                    {
                        var items = order.OrderDetails.Select(od => od.PlantId).ToList();
                        if (items.Any())
                        {
                            transactions.Add(new TransactionData
                            {
                                OrderId = order.Id,
                                Items = items
                            });
                        }
                    }
                }

                // Nếu không có đơn hàng của người dùng hoặc không có userId, lấy các đơn hàng mới nhất từ hệ thống
                if (!transactions.Any())
                {
                    _logger.LogInformation("Không có đơn hàng của người dùng, lấy đơn hàng gần đây từ hệ thống");

                    var completedOrders = _context.Orders
                        .Where(o => o.Status == OrderStatus.Delivered)
                        .Include(o => o.OrderDetails)
                        .OrderByDescending(o => o.OrderDate)
                        .Take(200)
                        .ToList();

                    foreach (var order in completedOrders)
                    {
                        var items = order.OrderDetails.Select(od => od.PlantId).ToList();
                        if (items.Any())
                        {
                            transactions.Add(new TransactionData
                            {
                                OrderId = order.Id,
                                Items = items
                            });
                        }
                    }
                }

                _logger.LogInformation("Đã lấy tổng cộng {Count} giao dịch từ database", transactions.Count);
                return transactions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy dữ liệu giao dịch: {Message}", ex.Message);
                return new List<TransactionData>();
            }
        }

        public async Task<List<Plant>> GetRecommendations(List<int> basketItems, int? userId, int topN = 5)
        {
            try
            {
                if (basketItems == null || !basketItems.Any())
                {
                    _logger.LogWarning("Không có sản phẩm trong basket để gợi ý");
                    return new List<Plant>();
                }

                _logger.LogInformation("Đang tìm gợi ý cho {Count} sản phẩm (IDs: {ItemIds})",
                    basketItems.Count, string.Join(", ", basketItems));

                var transactions = GetTransactionData(userId);
                _logger.LogInformation("Lấy được {Count} giao dịch để phân tích", transactions.Count);

                if (!transactions.Any())
                {
                    _logger.LogWarning("Không có dữ liệu giao dịch nào cho phân tích");
                    return new List<Plant>();
                }

                var inputData = new
                {
                    transactions = transactions,
                    basket = basketItems,
                    min_support = 0.001, // Giảm ngưỡng support để có nhiều pattern hơn
                    min_confidence = 0.1, // Giảm ngưỡng confidence để có nhiều kết quả hơn
                    top_n = topN
                };

                string jsonInput = JsonConvert.SerializeObject(inputData);
                _logger.LogInformation("JSON input độ dài: {Length} bytes", jsonInput.Length);

                // Chạy Python script để lấy gợi ý
                string pythonResult = await RunPythonScript(jsonInput);

                if (string.IsNullOrEmpty(pythonResult))
                {
                    _logger.LogWarning("Python script không trả về kết quả, thử phương pháp dự phòng");
                    return await GetFallbackRecommendations(basketItems, topN);
                }

                var result = JsonConvert.DeserializeObject<RecommendationResult>(pythonResult);

                if (result?.Recommendations == null || !result.Recommendations.Any())
                {
                    _logger.LogWarning("Không có gợi ý từ Python script, thử phương pháp dự phòng");
                    return await GetFallbackRecommendations(basketItems, topN);
                }

                _logger.LogInformation("Python script trả về {Count} gợi ý ID: {RecommendedIds}",
                    result.Recommendations.Count, string.Join(", ", result.Recommendations));

                var recommendedProducts = await _context.Plants
                    .Where(p => result.Recommendations.Contains(p.Id) && p.IsActive)
                    .ToListAsync();

                _logger.LogInformation("Tìm thấy {Count} sản phẩm active từ gợi ý", recommendedProducts.Count);

                // Nếu không đủ sản phẩm gợi ý, bổ sung thêm từ phương pháp dự phòng
                if (recommendedProducts.Count < topN)
                {
                    int remaining = topN - recommendedProducts.Count;
                    _logger.LogInformation("Cần bổ sung thêm {Count} sản phẩm", remaining);

                    // Lấy IDs của sản phẩm đã có để loại trừ
                    var existingIds = recommendedProducts.Select(p => p.Id).Concat(basketItems).ToList();

                    // Lấy sản phẩm bổ sung từ phương pháp dự phòng
                    var additionalProducts = await GetFallbackRecommendations(basketItems, remaining * 2);

                    // Lọc bỏ sản phẩm đã có
                    additionalProducts = additionalProducts
                        .Where(p => !existingIds.Contains(p.Id))
                        .Take(remaining)
                        .ToList();

                    _logger.LogInformation("Đã bổ sung thêm {Count} sản phẩm từ phương pháp dự phòng",
                        additionalProducts.Count);

                    // Thêm vào danh sách gợi ý
                    recommendedProducts.AddRange(additionalProducts);
                }

                return recommendedProducts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nghiêm trọng khi lấy gợi ý sản phẩm: {Message}", ex.Message);
                return await GetFallbackRecommendations(basketItems, topN);
            }
        }

        // Phương pháp dự phòng khi Python recommender không hoạt động
        private async Task<List<Plant>> GetFallbackRecommendations(List<int> basketItems, int topN)
        {
            try
            {
                _logger.LogInformation("Đang sử dụng phương pháp dự phòng nâng cao cho gợi ý sản phẩm");

                // Lấy danh sách sản phẩm cùng danh mục
                var items = await _context.Plants
                    .Where(p => basketItems.Contains(p.Id))
                    .ToListAsync();

                if (!items.Any())
                {
                    _logger.LogWarning("Không tìm thấy sản phẩm gốc để gợi ý");
                    return new List<Plant>();
                }

                // Lấy tất cả categoryId từ các sản phẩm trong basket
                var categoryIds = items.Select(p => p.CategoryId).Distinct().ToList();

                _logger.LogInformation("Tìm các sản phẩm từ {Count} danh mục: {Categories}",
                    categoryIds.Count, string.Join(", ", categoryIds));

                // Lấy tất cả sản phẩm cùng danh mục nhưng không có trong basket
                var similarProducts = await _context.Plants
                    .Where(p => categoryIds.Contains(p.CategoryId) &&
                               !basketItems.Contains(p.Id) &&
                               p.IsActive)
                    .OrderByDescending(p => p.Id) // Ưu tiên sản phẩm mới nhất
                    .Take(topN)
                    .ToListAsync();

                _logger.LogInformation("Phương pháp dự phòng: tìm thấy {Count} sản phẩm từ cùng danh mục",
                    similarProducts.Count);

                // Nếu vẫn không có đủ sản phẩm, bổ sung thêm các sản phẩm phổ biến
                if (similarProducts.Count < topN)
                {
                    int remainingCount = topN - similarProducts.Count;
                    _logger.LogInformation("Bổ sung thêm {Count} sản phẩm phổ biến", remainingCount);

                    // Lấy các ID sản phẩm đã chọn để loại trừ
                    var selectedIds = similarProducts.Select(p => p.Id).Concat(basketItems).ToList();

                    // Lấy sản phẩm phổ biến (dựa trên số lượng đơn hàng)
                    var popularProducts = await _context.OrderDetails
                        .GroupBy(od => od.PlantId)
                        .Select(g => new { PlantId = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .Take(remainingCount * 2) // Lấy nhiều hơn để có lựa chọn
                        .Select(x => x.PlantId)
                        .ToListAsync();

                    if (popularProducts.Any())
                    {
                        // Lấy thông tin chi tiết của sản phẩm phổ biến
                        var additionalProducts = await _context.Plants
                            .Where(p => popularProducts.Contains(p.Id) &&
                                      !selectedIds.Contains(p.Id) &&
                                      p.IsActive)
                            .Take(remainingCount)
                            .ToListAsync();

                        _logger.LogInformation("Đã tìm thêm {Count} sản phẩm phổ biến", additionalProducts.Count);
                        similarProducts.AddRange(additionalProducts);
                    }
                }

                return similarProducts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi sử dụng phương pháp dự phòng");
                return new List<Plant>();
            }
        }

        private async Task<string> RunPythonScript(string jsonInput)
        {
            string scriptPath = Path.Combine(_environment.ContentRootPath, "Scripts", "prepost_recommender.py");

            if (!File.Exists(scriptPath))
            {
                _logger.LogError("Python script không tìm thấy tại: {ScriptPath}", scriptPath);
                return null;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"\"{scriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _logger.LogInformation("Đang chạy Python script với input: {JsonInput}", jsonInput);

            try
            {
                using var process = new Process { StartInfo = startInfo };
                process.Start();

                // Gửi input vào stdin
                await process.StandardInput.WriteAsync(jsonInput);
                process.StandardInput.Close();

                // Đọc output/error đầy đủ
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                // Log kết quả từ Python script
                _logger.LogInformation("Python script hoàn thành, output length: {Length}",
                    string.IsNullOrEmpty(output) ? 0 : output.Length);

                if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogWarning("Python script lỗi hoặc cảnh báo: {Error}", error);
                }

                if (string.IsNullOrWhiteSpace(output))
                {
                    _logger.LogWarning("Python script không trả về output");
                    return null;
                }

                return output.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chạy Python script: {Message}", ex.Message);
                return null;
            }
        }

        // Các lớp hỗ trợ cho việc chuyển đổi dữ liệu
        public class TransactionData
        {
            public int OrderId { get; set; }
            [JsonProperty("Items")]
            public List<int> Items { get; set; } = new List<int>();
        }

        public class RecommendationResult
        {
            [JsonProperty("recommendations")]
            public List<int> Recommendations { get; set; } = new List<int>();
        }
    }
}
