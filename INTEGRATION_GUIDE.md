# Excel 格式轉換系統 - 整合指南

## 📋 概述
本系統將 OCR 辨識結果轉換為標準企業 Excel 格式（406INF 或 407INF），支援前端選擇、自動生成及下載。

---

## 🏗️ 系統架構

```
┌─────────────────────────────────────────────────────────────┐
│  前端 (HTML/JavaScript)                                      │
│  invoice_format_converter.html                               │
│  ├─ 格式選擇 (406INF / 407INF)                              │
│  ├─ 參數輸入 (Batch Name / Agent Name)                      │
│  └─ Excel 下載                                               │
└───────────────┬─────────────────────────────────────────────┘
                │ POST /api/convert-invoice-to-excel
                │ (ocrJson, format, paramValue)
                ↓
┌─────────────────────────────────────────────────────────────┐
│  後端 API (Azure Functions / 本地測試)                       │
│  ConvertInvoiceToExcel.cs                                    │
│  ├─ 接收 JSON 請求                                          │
│  ├─ 呼叫 InvoiceExcelConverter                             │
│  └─ 返回 Excel 二進制檔案                                   │
└───────────────┬─────────────────────────────────────────────┘
                │ Excel 轉換邏輯
                ↓
┌─────────────────────────────────────────────────────────────┐
│  轉換引擎 (C#)                                               │
│  InvoiceExcelConverter.cs                                    │
│  ├─ ConvertToExcel406() - 36 columns                        │
│  ├─ ConvertToExcel407() - 30 columns                        │
│  └─ EPPlus 格式化                                           │
└─────────────────────────────────────────────────────────────┘
```

---

## 📁 檔案清單

### 前端
- **invoice_format_converter.html** (新建)
  - 格式選擇介面（卡片設計）
  - JSON 預覽區
  - 參數設置
  - 下載按鈕

### 後端
- **ConvertInvoiceToExcel.cs** (新建)
  - Azure Function 入口點
  - HTTP POST 端點: `/api/convert-invoice-to-excel`
  - 請求驗證和錯誤處理

- **InvoiceExcelConverter.cs** (已改進)
  - 核心轉換邏輯
  - 406INF 格式: 36 columns
  - 407INF 格式: 30 columns
  - EPPlus 格式化

### 本地開發
- **local_server.py** (新建)
  - Python HTTP 伺服器
  - 支援跨域 CORS
  - 代理 Functions 呼叫

---

## 🚀 快速開始

### 1. 本地開發環境

#### 終端 1: 啟動 Azure Functions
```bash
cd /Users/chentungching/Documents/精誠軟體服務/威健/CODE
func host start
```
✅ Functions 應在 `http://localhost:7071` 啟動

#### 終端 2: 啟動 Python 伺服器
```bash
python3 local_server.py
```
✅ 伺服器應在 `http://localhost:8000` 啟動

#### 瀏覽器
```
http://localhost:8000/invoice_format_converter.html
```

### 2. 工作流程

1. **載入 JSON** (自動或貼上)
   - 點擊「📥 載入測試資料」載入範例
   - 或直接貼上 OCR JSON

2. **選擇格式**
   - 406INF：採購 + 收貨
   - 407INF：發票 + 應付

3. **設定參數** (可選)
   - 406INF: Batch Name
   - 407INF: Agent Name

4. **下載 Excel**
   - 點擊「💾 下載 Excel」
   - 自動下載 Excel 檔案

---

## 📊 格式說明

### 406INF 格式 (36 columns)
採購訂單整合格式，適用於：
- 採購流程管理
- 庫存追蹤
- 收貨驗收

**關鍵欄位:**
| 欄位 | 內容 | 來源 |
|------|------|------|
| Batch_Name | 批次名稱 | 參數 (預設: AUTO_GENERATED) |
| Vendor Num | 供應商編號 | OCR: seller |
| PO No | 採購單號 | OCR: item.poNo |
| Item No | 品項號 | OCR: item.itemNo |
| Qty | 數量 | OCR: item.quantity |
| Unit Price | 單價 | OCR: item.unitPrice |
| Amount | 金額 | OCR: item.amount |
| INV NO | 發票號 | OCR: invoiceNo |
| Invoice Date | 發票日期 | OCR: date |

### 407INF 格式 (30 columns)
供應商發票格式，適用於：
- 應付帳款管理
- 稅務申報
- 發票控制

**關鍵欄位:**
| 欄位 | 內容 | 來源 |
|------|------|------|
| Vendor_doc_num | 供應商文件號 | OCR: invoiceNo |
| PO_NUM | 採購單號 | OCR: item.poNo |
| Item | 品項描述 | OCR: item.description |
| Agent Name | 代理人 | 參數 (預設: TW1411) |
| Quantity | 數量 | OCR: item.quantity |
| Price | 單價 | OCR: item.unitPrice |
| Amount | 金額 | OCR: item.amount |

---

## 🔧 API 規格

### 端點
```
POST /api/convert-invoice-to-excel
```

### 請求格式
```json
{
  "ocrJson": "{\"invoiceNo\":\"...\",\"items\":[...]}",
  "format": "406",
  "paramValue": "AUTO_GENERATED"
}
```

### 回應
**成功 (200)**
```
Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
Content-Disposition: attachment; filename=Invoice_406INF_20250106120000.xlsx
[Excel 二進制資料]
```

**失敗 (400/500)**
```json
{
  "error": "Invalid request format"
}
```

---

## 📝 OCR JSON 格式

```json
{
  "invoiceNo": "2025110601",
  "date": "2025-11-06",
  "seller": "Vendor Name",
  "totalAmount": 39216.30,
  "currency": "USD",
  "items": [
    {
      "itemNo": "10",
      "poNo": "750185061",
      "description": "Product Description",
      "quantity": 9000,
      "unitPrice": 0.332,
      "amount": 2988.00
    }
  ]
}
```

---

## 🧪 測試

### 使用範例 JSON (6 個實際發票)

位置: `/Users/chentungching/Documents/精誠軟體服務/威健/CODE/ocr_results/`

1. **invoice_001_gaoshengda.json** (11 items)
   - USD $212,825.81
   - 複雜多品項

2. **invoice_002_sinpower.json** (2 items)
   - USD $2,246.40
   - 簡單結構

3. **invoice_003_forcelead.json** (1 item)
   - USD $578.55
   - 最簡結構

4. **invoice_004_celefide.json** (2 items)
   - USD $76,194.60
   - 中文混合

5. **invoice_005_bowltech_taiwan.json** (1 item)
   - TWD $1,102,400
   - 台灣格式

6. **invoice_006_gigadevice.json** (7 items)
   - USD $39,216.30
   - IC 元器件

### 測試步驟

1. 打開 `http://localhost:8000/invoice_format_converter.html`
2. 自動載入測試資料
3. 選擇 406INF 格式 → 下載
4. 選擇 407INF 格式 → 下載
5. 比較輸出與 `/INV_output/` 中的範本

---

## 🔍 故障排除

### 問題 1: Cannot connect to localhost:7071
```
❌ 錯誤: Cannot connect to local Functions host
```

**解決:**
```bash
# 終端 1 中執行
func host start
```

### 問題 2: EPPlus License Error
```
❌ LicenseContext property is not set
```

**解決:**
已在代碼中添加:
```csharp
EPPlus.LicenseContext = LicenseContext.NonCommercial;
```

### 問題 3: JSON 格式錯誤
```
❌ Invalid JSON format
```

**檢查:**
- OCR JSON 必須包含 `invoiceNo`, `items` 陣列
- `items` 需要 `quantity`, `unitPrice`, `amount`

### 問題 4: 下載失敗
```
❌ Conversion failed: ...
```

**檢查:**
- 檢查瀏覽器控制台錯誤
- 檢查 Functions 應用程式 Insights 日誌
- 確認參數格式正確

---

## 📈 性能考量

| 指標 | 值 |
|------|-----|
| 檔案大小 (406INF) | ~50 KB (100 items) |
| 檔案大小 (407INF) | ~40 KB (100 items) |
| 轉換時間 | <1 sec (10 items) |
| 記憶體使用 | ~10 MB per conversion |

---

## 🔐 安全性

- ✅ 輸入驗證 (JSON schema 檢查)
- ✅ 格式驗證 (只允許 "406" 或 "407")
- ✅ 錯誤隔離 (單行錯誤不影響整體)
- ✅ CORS 配置 (允許跨域本地測試)

### 生產環境建議

1. 啟用身份認證 (Azure AD)
2. 添加速率限制
3. 實施日誌記錄和監控
4. 使用 HTTPS

---

## 🚀 後續改進

- [ ] 支援多張工作表 (一個 invoice = 一個工作表)
- [ ] 批量轉換 (一次處理多個 JSON)
- [ ] 樣板自訂 (上傳自訂 Excel 樣板)
- [ ] 欄位對應自訂
- [ ] 預覽功能 (下載前預覽)
- [ ] 歷史記錄追蹤
- [ ] Webhook 通知

---

## 📞 支援

### 檔案位置
```
工作目錄: /Users/chentungching/Documents/精誠軟體服務/威健/CODE/
├── invoice_format_converter.html  (前端)
├── ConvertInvoiceToExcel.cs       (API)
├── InvoiceExcelConverter.cs       (轉換邏輯)
├── local_server.py                (本地伺服器)
├── ocr_results/                   (測試資料)
└── INV_output/                    (參考樣本)
```

### 常用命令
```bash
# 編譯
dotnet build

# 本地測試
func host start

# 發布
dotnet publish -c Release

# 清潔
dotnet clean
```

---

**版本:** 1.0  
**最後更新:** 2025-01-06  
**作者:** AI Assistant
