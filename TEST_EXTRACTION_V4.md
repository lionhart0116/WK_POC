# 表格提取測試 - V4 改進版

## 🔄 前後對比

### ❌ 之前的結果 (v2/v3)

```json
"items": [
  {
    "lineNo": "1",
    "quantity": null,           // ❌ 應該是 9720
    "unit": null,                // ❌ 應該是 PCS
    "itemNo": "Description",     // ❌ 錯誤的 itemNo
    "description": null,
    "unitPrice": null,
    "amount": 9720,              // ❌ 這個應該是 quantity
    "poNo": null
  },
  {
    "lineNo": "2",
    "quantity": null,            // ❌ 應該是 2124
    "unit": null,
    "itemNo": null,
    "description": null,
    "unitPrice": 2.3461,
    "amount": 2124,              // ❌ 這個應該是 quantity
    "poNo": "750189359"
  }
]
```

**問題分析:**
1. **數字完全映射錯誤** - quantity 被放到 amount 欄位
2. **沒有取得 itemNo/description** - 只有第一項有 "Description" 文字
3. **缺少 unit 資訊** - 所有 "PCS" 單位都沒被提取

---

### ✅ V4 改進後的結果 (預期)

```json
"items": [
  {
    "lineNo": "1",
    "quantity": 9720,            // ✅ 正確
    "unit": "PCS",               // ✅ 正確
    "itemNo": "WL6WR1510",       // ✅ 正確
    "description": "WLGWR1510-DAIKIN", // ✅ 正確
    "unitPrice": 3.2208,         // ✅ 正確
    "amount": 31306.18,          // ✅ 正確
    "poNo": "750190684"          // ✅ 正確
  },
  {
    "lineNo": "2",
    "quantity": 2124,            // ✅ 正確
    "unit": "PCS",               // ✅ 正確
    "itemNo": "WCT2GM2511",      // ✅ 正確
    "description": "WCT2GM2511-TPV", // ✅ 正確
    "unitPrice": 2.3461,         // ✅ 正確
    "amount": 4983.12,           // ✅ 計算或直接提取
    "poNo": "750189359"          // ✅ 正確
  },
  // ... 11 項
]
```

---

## 🧪 V4 算法說明

### Step 1: 分割表格範圍
```
fullText.substring(
  fullText.indexOf('QUANTITY'),  // 從此開始
  fullText.indexOf('TOTAL')       // 到此結束
)
```

### Step 2: 逐行掃描
```
tableLines = [
  "Quantity Unit Price Amount(USD) PO.NO",    // 標題（i=0，跳過）
  "1 9720 PCS",                               // i=1: 行號開始
  "Item No. WL6WR1510",                       // i=2: itemNo
  "Description",                              // i=3: 文字(可能是desc)
  "WLGWR1510-DAIKIN",                         // i=4: description
  "3.2208",                                   // i=5: unitPrice
  "31306.18",                                 // i=6: amount
  "PO.NO 750190684",                          // i=7: poNo
  "2 2124 PCS",                               // i=8: 下一行號開始
  ...
]
```

### Step 3: 狀態機邏輯

**Phase A:** 掃描到行號 `1 9720 PCS`
- `lineNo = "1"`
- `quantity = 9720`, `unit = "PCS"`
- 收集後續行: [i=2 到 i=7]

**Phase B:** 從後續行中逐個提取欄位
- `i=2: "Item No. WL6WR1510"` → 不是純數字，匹配 A-Z 開頭 → `itemNo = "Item No. WL6WR1510"` (或過濾 "Item No."？)
- `i=3: "Description"` → 文字，已有 itemNo，所以 `description = "Description"`? (實際應該是下一行)
- `i=4: "WLGWR1510-DAIKIN"` → 文字，已有 itemNo 和 desc？
- `i=5: "3.2208"` → 純數字，< 1000 → `unitPrice = 3.2208`
- `i=6: "31306.18"` → 純數字，>1000 → `amount = 31306.18`
- `i=7: "750190684"` 或 `"PO.NO 750190684"` → 9位數字 → `poNo = "750190684"`

**Phase C:** 對下一行號 `2 2124 PCS` 重複...

---

## 🔧 潛在優化

### Issue 1: itemNo vs Description 區分

在上面的 fullText 中:
```
Item No. WL6WR1510
Description
WLGWR1510-DAIKIN
```

目前 V4 會讀取:
- Line "Item No. WL6WR1510" → `itemNo = "Item No. WL6WR1510"` (包含標籤)
- Line "Description" → `description = "Description"` (只是標籤)
- Line "WLGWR1510-DAIKIN" → 會被忽略？因為已經有了 itemNo 和 description

**解決方案:**
```javascript
// 過濾掉標籤行
if (trimmed === "Description" || trimmed === "Item No." || trimmed === "Item No") {
    continue;  // 跳過純標籤行
}
```

### Issue 2: 混合行的處理

例如 `"PO.NO 750190684"` 這一行:
- 當前正則式可能抓不到 PO 號碼
- 建議額外提取: `content.match(/(\d{9})/)` 

---

## ✅ 測試步驟

1. 重新上傳 PDF (高盛達科技香港 INV.pdf)
2. 檢查 DevTools → localStorage → ocrData._simplified.extracted.items
3. 驗證每一項的 quantity, unit, itemNo, description, unitPrice, amount, poNo
4. 對比預期結果

---

## 🐛 若仍有問題

檢查點:
- [ ] fullText 中是否真的包含 "QUANTITY" 字樣？
- [ ] 表格是否真的以 "TOTAL" 結尾？
- [ ] 是否需要處理大小寫問題？ (toUpperCase() 應該已處理)
- [ ] itemNo/description 行是否被正確識別？

若需要進一步調試，建議添加 console.log:
```javascript
console.log("tableContent excerpt:", tableContent.substring(0, 500));
console.log("tableLines count:", tableLines.length);
tableLines.forEach((l, i) => console.log(`Line ${i}: ${l}`));
```

