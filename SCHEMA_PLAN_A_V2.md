# 方案 A 改進版 - 資料結構規範

## 概述
改進版方案 A 保留完整的表格行項目詳細資訊，並增加供應商、交易條款等關鍵欄位。

## 改進項目
✅ 保留所有表格行項目（11 行每行詳細資訊）
✅ 增加供應商和地址資訊
✅ 增加交易條款（Trade Term）和原產地
✅ 增加聯繫電話
✅ 表格每行保留：lineNo, quantity, unit, itemNo, description, unitPrice, amount, poNo

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
    // 發票基本資訊
    invoiceNo: "2020066251106A",
    date: "2025/11/6",
    
    // 供應商資訊
    seller: "GAOSHENGDA TECHNOLOGY HONGKONG CO., LIMITED",
    sellerAddress: "B/F BLDG 22R 3 SCIENCE PARK...",
    contact: "(0755)82943322-345",
    
    // 買方資訊
    buyer: "威健实业股份有限公司",
    
    // 交易條款
    tradeTerm: "FOB CHINA",
    origin: "中国/No Brand",
    
    // 表格行項目（完整保留）
    items: [
      {
        lineNo: "1",
        quantity: 9720,
        unit: "PCS",
        itemNo: "WL6WR1510",
        description: "WLGWR1510-DAIKIN",
        unitPrice: 3.2208,
        amount: 31306.18,
        poNo: "750190684"
      },
      {
        lineNo: "2",
        quantity: 2124,
        unit: "PCS",
        itemNo: "WCT2GM2511",
        description: "WCT2GM2511-TPV",
        unitPrice: 2.3461,
        amount: 4983.12,
        poNo: "750189359"
      },
      // ... 更多行項目 (3-11)
    ],
    
    // 總計
    totalAmount: 212825.81,
    currency: "USD"
  },
  
  fullText: "GAOSHENGDA TECHNOLOGY...[完整提取的文字]"
}
```

## 欄位詳解

### meta (元資料)
- `apiVersion`: Document Intelligence API 版本
- `model`: 使用的 AI 模型
- `pageCount`: 文件頁數
- `timestamp`: 處理時間戳

### extracted (提取的詳細欄位)

**發票基本資訊**
- `invoiceNo`: 發票號碼
- `date`: 發票日期

**供應商資訊**
- `seller`: 賣方公司名稱
- `sellerAddress`: 賣方地址
- `contact`: 聯繫電話

**買方資訊**
- `buyer`: 買方名稱

**交易條款**
- `tradeTerm`: 貿易條款 (如 FOB CHINA)
- `origin`: 原產地

**表格行項目（items 陣列）**
每個項目包含：
- `lineNo`: 行號 (1-11)
- `quantity`: 數量 (9720, 2124等)
- `unit`: 單位 (PCS)
- `itemNo`: 產品號碼 (WL6WR1510, WCT2GM2511等)
- `description`: 產品描述 (WLGWR1510-DAIKIN等)
- `unitPrice`: 單價 (3.2208, 2.3461等)
- `amount`: 金額 (31306.18, 4983.12等)
- `poNo`: PO 號碼 (750190684等)

**金額和幣別**
- `totalAmount`: 總金額 (212825.81)
- `currency`: 幣別代碼 (USD)

### fullText
完整提取的文字內容，用於全文搜尋

## 相比改進前的改進

| 項目 | 改進前 | 改進後 |
|------|-------|-------|
| 表格行項目 | 只有行號和簡單描述 | 完整保留 lineNo, qty, unit, itemNo, description, unitPrice, amount, poNo |
| 供應商資訊 | 無 | seller, sellerAddress, contact |
| 交易條款 | 無 | tradeTerm, origin |
| 文件大小 | ~5KB | ~10-15KB (但資訊完整得多) |
| 適用場景 | 基本查詢 | 完整發票重現、財務系統導入、報表生成 |

## 使用場景
- 資料庫存儲（完整記錄）
- 財務系統導入
- 發票驗證和對帳
- 報表生成
- 批量處理

## Console 日誌
在瀏覽器 DevTools Console 會看到：
```
【方案 A 改進版 - 詳細提取資料】 { meta: {...}, extracted: {...}, fullText: "..." }
【原始 Document Intelligence 回應】 { analyzeResult: {...} }
```
