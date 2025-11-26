# 方案 A：精簡提取法 - 資料結構規範

## 概述
將龐大的 Document Intelligence API 回應 (~1000+ 行) 轉換為精簡、結構化的資料格式，用於存儲、查詢和傳輸。

## 輸出結構

```javascript
{
  meta: {
    apiVersion: "2024-02-29-preview",
    model: "prebuilt-read",
    pageCount: 1,
    timestamp: "2025-11-25T12:34:56.789Z"
  },
  
  extracted: {
    // 發票類型時：
    invoiceNo: "2020066251106A",
    date: "2025/11/6",
    buyer: "威健实业股份有限公司",
    totalAmount: 212825.81,
    currency: "USD",
    items: [
      {
        lineNo: "1",
        description: "WLGWR1510-DAIKIN"
      }
    ]
    
    // 訂單類型時：
    orderNo: "PO750190684",
    date: "2025/11/6",
    quantity: 9720
  },
  
  fullText: "GAOSHENGDA TECHNOLOGY...[完整提取的文字]"
}
```

## 欄位說明

### meta (元資料)
- `apiVersion`: Document Intelligence API 版本
- `model`: 使用的 AI 模型
- `pageCount`: 文件頁數
- `timestamp`: 處理時間戳

### extracted (提取的關鍵欄位)
**發票類型字段:**
- `invoiceNo`: 發票號碼（支持多格式）
- `date`: 發票日期 (YYYY/MM/DD)
- `buyer`: 買方名稱
- `totalAmount`: 總金額（數值類型）
- `currency`: 幣別代碼 (USD, TWD等)
- `items`: 行項目陣列

**訂單類型字段:**
- `orderNo`: 訂單/PO號碼
- `date`: 訂單日期
- `quantity`: 數量

### fullText
完整提取的文字內容（去除格式），用於全文搜尋或備份

## 優點
✅ 文件大小減少 90%+ (從 ~200KB 降至 ~5KB)
✅ 易於存儲至資料庫 (JSON 相容)
✅ 快速查詢和訪問
✅ 易於序列化和傳輸

## 使用場景
- 資料庫存儲
- API 回應
- 批量處理
- 報表生成

## 完整 API 回應去向
保留在 `ocrData._raw` 中，當需要時可切換至「原始版」標籤頁查看完整信息。
