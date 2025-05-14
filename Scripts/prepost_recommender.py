# -*- coding: utf-8 -*-
import sys
import json
from collections import defaultdict, namedtuple

ItemInfo = namedtuple("ItemInfo", ["pre", "post", "count"])

class PPCNode:
    def __init__(self, item, parent=None):
        self.item = item
        self.count = 1
        self.parent = parent
        self.children = {}
        self.pre = None
        self.post = None

    def increment(self, count=1):
        self.count += count

class PPCTree:
    def __init__(self):
        self.root = PPCNode(None)
        self.pre_counter = 1
        self.post_counter = 1
        self.item_nlist = defaultdict(list)

    def add_transaction(self, transaction):
        node = self.root
        for item in transaction:
            if item not in node.children:
                node.children[item] = PPCNode(item, parent=node)
            else:
                node.children[item].increment()
            node = node.children[item]

    def assign_prepost(self):
        def dfs(node):
            node.pre = self.pre_counter
            self.pre_counter += 1
            for child in node.children.values():
                dfs(child)
            node.post = self.post_counter
            self.post_counter += 1
            if node.item is not None:
                self.item_nlist[node.item].append(ItemInfo(node.pre, node.post, node.count))
        dfs(self.root)

def is_ancestor(a, b):
    return a.pre < b.pre and a.post > b.post

def intersect_nlist(nlist1, nlist2):
    result = []
    for i in nlist1:
        for j in nlist2:
            if is_ancestor(j, i):
                result.append(ItemInfo(i.pre, i.post, min(i.count, j.count)))
    return result

def mine_patterns(nlists, min_support):
    patterns = {}
    # Thêm các mẫu 1 item với support >= min_support
    for item, nlist in nlists.items():
        total = sum(e.count for e in nlist)
        if total >= min_support:
            patterns[(item,)] = total

    # Thêm các mẫu 2 item
    items = list(nlists.keys())
    for i in range(len(items)):
        for j in range(i + 1, len(items)):
            i1, i2 = items[i], items[j]
            intersected = intersect_nlist(nlists[i1], nlists[i2])
            support = sum(e.count for e in intersected)
            if support >= min_support:
                patterns[(i1, i2)] = support
                
    return patterns

def recommend_from_patterns(patterns, basket, top_n):
    scores = defaultdict(int)
    
    # Nếu basket chỉ có 1 item
    if len(basket) == 1:
        # Tìm các item thường xuất hiện cùng với basket item
        basket_item = basket[0]
        for items, support in patterns.items():
            if len(items) > 1:  # Chỉ xét các pattern có 2+ items
                if basket_item in items:
                    for item in items:
                        if item != basket_item:
                            scores[item] += support
        
        # Nếu không có gợi ý, thêm gợi ý dựa trên support của từng item
        if not scores:
            for items, support in patterns.items():
                if len(items) == 1 and items[0] != basket_item:
                    scores[items[0]] += support
    else:
        # Tìm các gợi ý dựa trên pattern chứa ít nhất 1 item trong basket
        for items, support in patterns.items():
            # Nếu ít nhất một item trong pattern có trong basket
            if any(b in items for b in basket):
                for item in items:
                    if item not in basket:
                        scores[item] += support
            
            # Nếu tất cả item trong basket đều nằm trong pattern
            if all(b in items for b in basket):
                for item in items:
                    if item not in basket:
                        # Tăng thêm điểm cho các item này
                        scores[item] += support * 2
    
    # Trả về top_n gợi ý có điểm cao nhất
    return [item for item, _ in sorted(scores.items(), key=lambda x: -x[1])][:top_n]

def read_input():
    try:
        input_str = sys.stdin.read()
        if not input_str:
            print(json.dumps({"recommendations": []}))
            sys.exit(0)
        return json.loads(input_str)
    except Exception as e:
        sys.stderr.write(f"Lỗi khi đọc input: {str(e)}\n")
        return None

def main():
    try:
        data = read_input()
        if not data:
            print(json.dumps({"recommendations": []}))
            sys.exit(0)
            
        transactions = data.get("transactions", [])
        basket = data.get("basket", [])
        min_support_percent = data.get("min_support", 0.005)  # Giảm min_support mặc định
        top_n = data.get("top_n", 5)

        # Debug info
        sys.stderr.write(f"Transactions: {len(transactions)}, Basket: {basket}\n")

        # Kiểm tra dữ liệu đầu vào
        if not transactions or not basket:
            print(json.dumps({"recommendations": []}))
            sys.exit(0)
            
        # Đếm tần suất của mỗi item
        item_counts = defaultdict(int)
        for t in transactions:
            for i in t["Items"]:
                item_counts[i] += 1
        
        # Debug thông tin về số lượng sản phẩm        
        sys.stderr.write(f"Unique items: {len(item_counts)}\n")
                
        # Tính toán min_support tuyệt đối 
        min_support = max(1, int(min_support_percent * len(transactions)))
        sys.stderr.write(f"Min support: {min_support}\n")
                
        # Lọc các item có support lớn hơn min_support
        freq_items = {k for k, v in item_counts.items() if v >= min_support}
        sys.stderr.write(f"Frequent items: {len(freq_items)}\n")
        
        # Nếu không có item nào đủ support, giảm min_support và thử lại
        if not freq_items:
            min_support = 1  # Min support = 1
            freq_items = {k for k, v in item_counts.items() if v >= min_support}
            sys.stderr.write(f"Reduced min support to 1, frequent items: {len(freq_items)}\n")
            
            if not freq_items:
                print(json.dumps({"recommendations": []}))
                sys.exit(0)

        # Xây dựng PPC-tree từ các giao dịch đã lọc
        tree = PPCTree()
        for txn in transactions:
            # Lọc các items có trong freq_items
            filtered = [i for i in txn["Items"] if i in freq_items]
            # Sắp xếp theo tần suất giảm dần
            if filtered:
                sorted_txn = sorted(filtered, key=lambda x: -item_counts[x])
                tree.add_transaction(sorted_txn)

        tree.assign_prepost()

        # Khai thác các mẫu phổ biến
        patterns = mine_patterns(tree.item_nlist, min_support)
        sys.stderr.write(f"Patterns found: {len(patterns)}\n")
        
        # Nếu không có pattern nào, trả về các item phổ biến
        if not patterns:
            popular_items = sorted([(item, count) for item, count in item_counts.items()], 
                               key=lambda x: -x[1])
            recommendations = [item for item, _ in popular_items if item not in basket][:top_n]
            sys.stderr.write(f"No patterns, using popular items: {recommendations}\n")
            print(json.dumps({"recommendations": recommendations}))
            sys.exit(0)

        # Gợi ý từ basket
        recommendations = recommend_from_patterns(patterns, basket, top_n)
        sys.stderr.write(f"Final recommendations: {recommendations}\n")
        
        # Nếu không có gợi ý, lấy top sản phẩm phổ biến
        if not recommendations:
            popular_items = sorted([(item, count) for item, count in item_counts.items()], 
                               key=lambda x: -x[1])
            recommendations = [item for item, _ in popular_items if item not in basket][:top_n]
            sys.stderr.write(f"No recommendations from patterns, using popular items: {recommendations}\n")
        
        # Trả kết quả
        print(json.dumps({"recommendations": recommendations}))
    except Exception as e:
        sys.stderr.write(f"Lỗi xử lý: {str(e)}\n")
        print(json.dumps({"recommendations": []}))

if __name__ == "__main__":
    main()
