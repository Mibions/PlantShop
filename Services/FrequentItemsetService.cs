using PlantShop.Data;
using System.Collections.Generic;
using System.Linq;

namespace PlantShop.Services
{
    public class FrequentItemsetService
    {
        private readonly ApplicationDbContext _context;
        private readonly int _minSupport;

        public FrequentItemsetService(ApplicationDbContext context, int minSupport = 2)
        {
            _context = context;
            _minSupport = minSupport;
        }

        // Lấy tất cả transaction (mỗi transaction là list PlantId)
        private List<List<int>> GetTransactions()
        {
            return _context.Orders
                .Where(o => o.Status == Models.OrderStatus.Delivered || o.Status == Models.OrderStatus.Processing || o.Status == Models.OrderStatus.Pending)
                .Select(o => o.OrderDetails.Select(od => od.PlantId).Distinct().ToList())
                .ToList();
        }

        // Tìm frequent itemsets (bậc 2, 3, 4...)
        public List<List<int>> GetFrequentItemsets(int maxLength = 3)
        {
            var transactions = GetTransactions();
            var result = new List<List<int>>();
            var itemsets = new Dictionary<string, int>();

            // Đếm từng item đơn lẻ
            var allItems = transactions.SelectMany(t => t).Distinct().ToList();

            // Đếm các tập con có độ dài từ 2 đến maxLength
            for (int length = 2; length <= maxLength; length++)
            {
                var candidates = new HashSet<string>();

                foreach (var t in transactions)
                {
                    if (t.Count < length) continue;
                    var subsets = GetSubsets(t, length);
                    foreach (var subset in subsets)
                    {
                        var key = string.Join(",", subset.OrderBy(x => x));
                        if (!itemsets.ContainsKey(key))
                            itemsets[key] = 0;
                        itemsets[key]++;
                        candidates.Add(key);
                    }
                }

                // Lọc các tập phổ biến
                foreach (var key in candidates)
                {
                    if (itemsets[key] >= _minSupport)
                    {
                        var ids = key.Split(',').Select(int.Parse).ToList();
                        result.Add(ids);
                    }
                }
            }

            return result;
        }

        // Lấy các tập con độ dài k từ 1 transaction
        private List<List<int>> GetSubsets(List<int> transaction, int k)
        {
            var result = new List<List<int>>();
            GetSubsetsHelper(transaction, k, 0, new List<int>(), result);
            return result;
        }

        private void GetSubsetsHelper(List<int> arr, int k, int index, List<int> current, List<List<int>> result)
        {
            if (current.Count == k)
            {
                result.Add(new List<int>(current));
                return;
            }
            for (int i = index; i < arr.Count; i++)
            {
                current.Add(arr[i]);
                GetSubsetsHelper(arr, k, i + 1, current, result);
                current.RemoveAt(current.Count - 1);
            }
        }

        // Gợi ý sản phẩm cho 1 hoặc nhiều sản phẩm đầu vào
        public List<int> GetRecommendations(List<int> inputProductIds, int top = 5)
        {
            var frequentSets = GetFrequentItemsets();
            var recommendations = new Dictionary<int, int>();

            foreach (var set in frequentSets)
            {
                // Nếu input là tập con của set, gợi ý các sản phẩm còn lại trong set
                if (inputProductIds.All(id => set.Contains(id)))
                {
                    foreach (var id in set)
                    {
                        if (!inputProductIds.Contains(id))
                        {
                            if (!recommendations.ContainsKey(id))
                                recommendations[id] = 0;
                            recommendations[id]++;
                        }
                    }
                }
            }

            return recommendations.OrderByDescending(x => x.Value).Take(top).Select(x => x.Key).ToList();
        }
    }
}