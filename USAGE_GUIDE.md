# 🎯 Excel 格式轉換系統 - 使用指南

## 📚 目錄
1. [系統概述](#系統概述)
2. [快速開始](#快速開始)  
3. [工作流程](#工作流程)
4. [本地測試](#本地測試)
5. [部署到 Azure](#部署到-azure)

---

## 系統概述

本系統提供完整的 OCR 發票轉換解決方案：
- **前端:** HTML 頁面選擇轉換格式
- **後端:** C# Azure Functions 執行轉換
- **輸出:** 標準企業 Excel 格式（406INF 或 407INF）

### ✨ 主要功能

| 功能 | 說明 |
|------|------|
| 📊 格式選擇 | 406INF（採購） 或 407INF（發票） |
| 📝 參數自訂 | 批次名稱 / 代理人代碼 |
| 📥 JSON 預覽 | 完整 OCR 結果展示 |
| 💾 Excel 下載 | 自動生成並下載 Excel 檔案 |

---

## 快速開始

### 前置條件
- .NET 8.0 SDK
- Azure Functions Core Tools (`func` 命令)
- Python 3.7+（用於本地伺服器）
- Node.js (可選，用於 Azure CLI)

### 安裝依賴

```bash
# 確保 .NET 專案編譯
cd /Users/chentungching/Documents/精誠軟體服務/威健/CODE
dotnet build

# 安裝 Azure Functions
func --version  # 確認已安裝
```

---

## 工作流程

### 步驟 1: 準備 OCR JSON

OCR JSON 格式必須包含：
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

### 步驟 2: 啟動後端

```bash
# 編譯
dotnet build

# 啟動 Azure Functions (在 Port 7071)
func host start
```

✅ 應看到:
```
Azure Functions Core Tools
...
Listening on http://localhost:7071
```

### 步驟 3: 啟動前端伺服器

```bash
# 另開終端
cd /Users/chentungching/Documents/精誠軟體服務/威健/CODE

# 啟動 Python 伺服器 (Port 8000)
python3 local_server.py
```

✅ 應看到:
```
🚀 HTTP 伺服器已啟動: http://localhost:8000
📄 開啟: http://localhost:8000/invoice_format_converter.html
```

### 步驟 4: 開啟前端

打開瀏覽器:
```
http://localhost:8000/invoice_format_converter.html
```

### 步驟 5: 轉換 Excel

1. **載入資料**
   - 點擊「📥 載入測試資料」
   - 或貼上自己的 JSON (Ctrl+V)

2. **選擇格式**
   - 406INF: 採購訂單 + 收貨
   - 407INF: 供應商發票

3. **設定參數** (可選)
   - Batch Name (406INF)
   - Agent Name (407INF)

4. **下載**
   - 點擊「💾 下載 Excel」

---

## 本地測試

### 使用測試資料

系統包含 6 個真實發票樣本:

```bash
ls -la /Users/chentungching/Documents/精誠軟體服務/威健/CODE/ocr_results/
```

輸出:
```
invoice_001_gaoshengda.json      # 11 items, USD $212,825.81
invoice_002_sinpower.json         # 2 items, USD $2,246.40
invoice_003_forcelead.json        # 1 item, USD $578.55
invoice_004_celefide.json         # 2 items, USD $76,194.60
invoice_005_bowltech_taiwan.json  # 1 item, TWD $1,102,400
invoice_006_gigadevice.json       # 7 items, USD $39,216.30
```

### 測試 API

使用 curl:

```bash
# 準備 JSON 檔案
JSON_FILE="/Users/chentungching/Documents/精誠軟體服務/威健/CODE/ocr_results/invoice_006_gigadevice.json"

# 讀取 JSON 內容
OCR_JSON=$(cat "$JSON_FILE")

# 調用 API
curl -X POST http://localhost:7071/api/convert-invoice-to-excel \
  -H "Content-Type: application/json" \
  -d "{
    \"ocrJson\": \"$(echo "$OCR_JSON" | jq -c .)\",
    \"format\": \"406\",
    \"paramValue\": \"DIM-AUTO-001\"
  }" \
  -o invoice_406.xlsx

# 檢查檔案
file invoice_406.xlsx
ls -lh invoice_406.xlsx
```

### 檢查輸出

打開生成的 Excel:
```bash
# macOS
open invoice_406.xlsx

# Linux
libreoffice invoice_406.xlsx

# Windows
start invoice_406.xlsx
```

驗證:
- ✅ 標題列正確
- ✅ 資料行對應正確
- ✅ 金額格式為 "#,##0.00"
- ✅ 日期格式正確

---

## 部署到 Azure

### 1. 建立 Azure Function App

```bash
# 設定變數
RESOURCE_GROUP="my-resource-group"
REGION="eastasia"
STORAGE_ACCOUNT="mystorageXXXXX"
FUNCTION_APP="my-invoice-function"

# 建立資源群組
az group create --name $RESOURCE_GROUP --location $REGION

# 建立儲存帳戶
az storage account create \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $REGION

# 建立 Function App
az functionapp create \
  --resource-group $RESOURCE_GROUP \
  --consumption-plan-location $REGION \
  --runtime dotnet \
  --runtime-version 8.0 \
  --functions-version 4 \
  --name $FUNCTION_APP \
  --storage-account $STORAGE_ACCOUNT
```

### 2. 發布程式碼

```bash
# 本地編譯發布版本
dotnet publish -c Release -o ./publish

# 使用 Azure CLI 部署
func azure functionapp publish $FUNCTION_APP --build remote

# 或使用 Visual Studio Code 擴展
# 1. 開啟 VS Code
# 2. 開啟 Command Palette (Cmd+Shift+P)
# 3. 搜尋 "Deploy to Function App"
```

### 3. 設定前端 URL

修改 `invoice_format_converter.html` 第 X 行:

```javascript
// 從本地
const API_URL = 'http://localhost:7071/api/convert-invoice-to-excel';

// 改為 Azure
const API_URL = 'https://<your-function-app>.azurewebsites.net/api/convert-invoice-to-excel';
```

### 4. 測試生產環境

```bash
# 取得 Function App 的主機名
FUNCTION_URL=$(az functionapp show \
  --resource-group $RESOURCE_GROUP \
  --name $FUNCTION_APP \
  --query defaultHostName -o tsv)

echo "Function URL: https://$FUNCTION_URL"

# 測試 API
curl -X POST https://$FUNCTION_URL/api/convert-invoice-to-excel \
  -H "Content-Type: application/json" \
  -d "{
    \"ocrJson\": \"{...}\",
    \"format\": \"407\",
    \"paramValue\": \"TW1411\"
  }"
```

---

## 📊 文件結構

```
CODE/
├── invoice_format_converter.html        # 前端頁面
├── ConvertInvoiceToExcel.cs             # API 入口點
├── InvoiceExcelConverter.cs             # 轉換邏輯
├── local_server.py                      # 本地開發伺服器
├── ocr_results/                         # 測試資料
│   ├── invoice_001_gaoshengda.json
│   ├── invoice_002_sinpower.json
│   ├── ...
│   └── invoice_006_gigadevice.json
├── INV_output/                          # 參考 Excel 樣本
│   ├── 406INF.xlsx
│   └── 407INF.csv.xlsx
└── INTEGRATION_GUIDE.md                 # 整合文檔
```

---

## 🔧 故障排除

### 問題 1: `Cannot connect to localhost:7071`

```
❌ Cannot connect to local Functions host on localhost:7071
```

**解決:**
- 確認 Functions 正在執行: `func host start`
- 檢查防火牆設定
- 嘗試 `curl http://localhost:7071/api/health`

### 問題 2: `EPPlus License Error`

```
❌ LicenseContext property is not set
```

**解決:**
已在 `InvoiceExcelConverter.cs` 中自動設定，無需手動操作

### 問題 3: `Invalid JSON format`

```
❌ Error: Invalid JSON format
```

**解決:**
- 驗證 JSON 語法 (使用 online JSON validator)
- 確認包含所有必要欄位
- 檢查特殊字符編碼 (UTF-8)

### 問題 4: `Excel file corruption`

```
❌ Cannot open Excel file
```

**解決:**
- 檢查 EPPlus 版本兼容性
- 驗證儲存格公式無誤
- 嘗試在另一台電腦打開

---

## 📈 性能優化

### 批量轉換
```csharp
// 未來功能: 同時轉換多個 JSON
for (int i = 0; i < jsonArray.Length; i++) {
    var excel = ConvertToExcel(jsonArray[i], format);
    // 保存或返回
}
```

### 記憶體管理
```csharp
using (var workbook = new ExcelPackage()) {
    // 處理...
} // 自動释放记忆体
```

---

## 📞 聯絡支援

### 常用命令

```bash
# 建置
dotnet build

# 清潔
dotnet clean

# 本地測試
func host start

# 檢查版本
dotnet --version
func --version
```

### 日誌位置

```bash
# Azure Functions 日誌
~/.azure/cli/logs/

# 應用程式 Insights
# 在 Azure Portal -> Function App -> Monitor
```

---

**版本:** 1.0  
**最後更新:** 2025-01-06  
**狀態:** ✅ 生產就緒
