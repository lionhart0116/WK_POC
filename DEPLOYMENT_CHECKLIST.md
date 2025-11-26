# Azure Function 部署檢查清單

## 版本升級：REST API → 官方 SDK

### ✅ 已完成的改動

1. **後端改動**
   - [x] 添加 NuGet 包：`Azure.AI.DocumentIntelligence v1.0.0`
   - [x] 新建 `UploadOcrSDK.cs` - SDK 實現版本
   - [x] 標記 `UploadOcr.cs` 為已棄用
   - [x] 編譯成功（0 errors, 0 warnings）

2. **環境配置**
   - [x] 更新 `local.settings.json` - 添加環境變數
   - [x] 更新 `deploy-azure.sh` - 部署時設定環境變數

3. **前端改動**
   - [x] 更新 `copilot_assistant.html` - 使用新端點 `upload-ocr-sdk`

### ⚠️ 部署前準備

#### 1. 確認 Document Intelligence 資源
```bash
# 檢查 Azure 上的 Document Intelligence 資源
az cognitiveservices account show \
  --name wk-doc-intelligence \
  --resource-group rg-wk-pdf-ocr
```

獲取 Endpoint 和 Key：
```bash
# 獲取 Endpoint
az cognitiveservices account show \
  --name wk-doc-intelligence \
  --resource-group rg-wk-pdf-ocr \
  --query "properties.endpoint" -o tsv

# 獲取 Key (Key1)
az cognitiveservices account keys list \
  --name wk-doc-intelligence \
  --resource-group rg-wk-pdf-ocr \
  --query "key1" -o tsv
```

#### 2. 更新本地測試環境
編輯 `local.settings.json`，使用實際的 endpoint 和 key：
```json
{
  "IsEncrypted": false,
  "Values": {
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AZ_DOCUMENT_INTELLIGENCE_ENDPOINT": "https://wk-doc-intelligence.cognitiveservices.azure.com/",
    "AZ_DOCUMENT_INTELLIGENCE_KEY": "YOUR_ACTUAL_KEY_HERE",
    "AZ_OCR_MAX_FILE_BYTES": "20000000",
    "AZ_OCR_POLL_MAX_ATTEMPTS": "40",
    "AZ_OCR_POLL_INITIAL_DELAY_MS": "500"
  }
}
```

#### 3. 本地測試（可選）
```bash
# 編譯
dotnet build

# 啟動本地 Function
func start

# 在另一個終端測試上傳
curl -X POST http://localhost:7071/api/upload-ocr-sdk \
  -H "x-functions-key: YOUR_KEY" \
  -F "file=@test_invoice.pdf"
```

### 🚀 部署步驟

#### 方案 A：使用 deploy-azure.sh（推薦）
```bash
# 確保已登入 Azure
az login

# 運行部署腳本
bash ./deploy-azure.sh

# 設定 Document Intelligence 金鑰
export DOCUMENT_INTELLIGENCE_KEY="your-key-here"
bash ./deploy-azure.sh
```

#### 方案 B：手動部署
```bash
# 1. 編譯發佈版本
dotnet publish --configuration Release --output ./publish

# 2. 部署到現有的 Function App
func azure functionapp publish func-wk-pdf-ocr

# 3. 設定環境變數
az functionapp config appsettings set \
  --name func-wk-pdf-ocr \
  --resource-group rg-wk-pdf-ocr \
  --settings \
    "AZ_DOCUMENT_INTELLIGENCE_ENDPOINT=https://wk-doc-intelligence.cognitiveservices.azure.com/" \
    "AZ_DOCUMENT_INTELLIGENCE_KEY=YOUR_KEY" \
    "AZ_OCR_MAX_FILE_BYTES=20000000"
```

### ✔️ 部署後驗證

1. **檢查 Function 是否上線**
```bash
az functionapp function show \
  --name func-wk-pdf-ocr \
  --resource-group rg-wk-pdf-ocr \
  --function-name upload-ocr-sdk
```

2. **測試新端點**
```bash
# 獲取 Function Key
az functionapp keys list \
  --name func-wk-pdf-ocr \
  --resource-group rg-wk-pdf-ocr

# 測試上傳
curl -X POST https://func-wk-pdf-ocr.azurewebsites.net/api/upload-ocr-sdk \
  -H "x-functions-key: YOUR_FUNCTION_KEY" \
  -F "file=@test_invoice.pdf"
```

3. **檢查日誌**
```bash
# 實時查看日誌
func azure functionapp logstream func-wk-pdf-ocr
```

### 📝 環境變數對應

| 環境變數 | 說明 | 取得方式 |
|---------|------|--------|
| `AZ_DOCUMENT_INTELLIGENCE_ENDPOINT` | Document Intelligence 資源的 endpoint | Azure Portal 或 `az cognitiveservices account show` |
| `AZ_DOCUMENT_INTELLIGENCE_KEY` | Document Intelligence 資源的 API Key | Azure Portal 或 `az cognitiveservices account keys list` |
| `AZ_OCR_MAX_FILE_BYTES` | 最大上傳文件大小 (bytes) | 預設：20MB |
| `AZ_OCR_POLL_MAX_ATTEMPTS` | 輪詢最大次數 | 預設：40 |
| `AZ_OCR_POLL_INITIAL_DELAY_MS` | 初始輪詢延遲 (ms) | 預設：500ms |

### 🔄 兼容性注意

- **舊端點** `upload-ocr` 仍然可用（已棄用）
- **新端點** `upload-ocr-sdk` 使用官方 SDK（推薦）
- 前端已配置為使用新端點

### 📱 前端更新

前端 (`copilot_assistant.html`) 已更新：
```javascript
const AZURE_FUNCTION_URL_SDK = 'https://wk-pdf-ocr.azurewebsites.net/api/upload-ocr-sdk';
const AZURE_FUNCTION_URL = AZURE_FUNCTION_URL_SDK;
```

### ⚡ 故障排查

如果遇到問題：

1. **404 錯誤 - Function 未找到**
   - 確保 `UploadOcrSDK.cs` 中的 `[Function("upload-ocr-sdk")]` 屬性正確
   - 重新部署代碼

2. **認證失敗**
   - 檢查 Document Intelligence Key 是否正確
   - 驗證 endpoint URL 是否完整且正確

3. **超時**
   - 檢查 Document Intelligence 服務是否正常運行
   - 增加 `AZ_OCR_POLL_MAX_ATTEMPTS` 值

4. **CORS 錯誤**
   - 確保已設定 CORS（已在部署腳本中處理）
   - 檢查允許的來源設定

---

**建議流程**：
1. ✅ 確認所有環境變數已準備
2. ✅ 在本地測試（可選）
3. ✅ 編譯無誤
4. ✅ 運行部署腳本
5. ✅ 驗證部署成功
6. ✅ 測試新 SDK 端點
