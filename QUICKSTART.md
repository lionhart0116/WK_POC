# 🚀 Excel 格式轉換系統 - 快速開始 (5 分鐘)

## ⚡ 1分鐘 Setup

### 終端 1: 啟動後端
```bash
cd /Users/chentungching/Documents/精誠軟體服務/威健/CODE
func host start
```
✅ 看到 `Listening on http://localhost:7071`

### 終端 2: 啟動前端伺服器
```bash
cd /Users/chentungching/Documents/精誠軟體服務/威健/CODE
python3 local_server.py
```
✅ 看到 `🚀 HTTP 伺服器已啟動: http://localhost:8000`

## 🌐 2分鐘 打開瀏覽器

```
http://localhost:8000/invoice_format_converter.html
```

## 📊 3分鐘 轉換 Excel

### 方式 A: 使用測試資料（推薦）

1. 頁面自動載入測試資料 ✅
2. 選擇格式：**406INF** 或 **407INF**
3. 點擊 **💾 下載 Excel**
4. 檔案自動下載 ✅

### 方式 B: 使用自己的 JSON

1. 打開你的 OCR JSON 檔案
2. 複製全部內容 (Ctrl+A, Ctrl+C)
3. 在頁面上貼上 (Ctrl+V)
4. 選擇格式 & 下載

## 📁 檔案位置

| 項目 | 路徑 |
|------|------|
| 前端 | `invoice_format_converter.html` |
| API | `ConvertInvoiceToExcel.cs` |
| 轉換邏輯 | `InvoiceExcelConverter.cs` |
| 本地伺服器 | `local_server.py` |
| 測試資料 | `ocr_results/` (6 個 JSON) |
| 參考 Excel | `INV_output/` (406/407 格式) |

## 🎯 測試清單

- [ ] 終端 1 看到 "Listening on http://localhost:7071"
- [ ] 終端 2 看到 "🚀 HTTP 伺服器已啟動"
- [ ] 瀏覽器打開 `http://localhost:8000/invoice_format_converter.html`
- [ ] 看到 JSON 預覽和測試資料自動載入
- [ ] 選擇 406INF 格式
- [ ] 點擊 "💾 下載 Excel"
- [ ] 檔案下載成功 ✅
- [ ] 用 Excel/Numbers 打開檔案，驗證資料正確

## 🔧 常見問題

### Q: 下載按鈕灰色？
A: 需要先選擇格式 (406INF 或 407INF)

### Q: 看到紅色錯誤？
A: 確認終端 1 的 Functions 還在執行 (沒有 CTRL+C)

### Q: JSON 無法貼上？
A: 檢查 JSON 格式是否有效，使用 `JSON.parse()` 測試

## 📈 下一步

- 📖 詳細設定: 見 `INTEGRATION_GUIDE.md`
- 🚀 部署 Azure: 見 `USAGE_GUIDE.md`
- 💻 自訂代碼: 編輯 `InvoiceExcelConverter.cs`

## 💡 提示

| 技巧 | 說明 |
|------|------|
| 406INF | 採購 + 收貨格式（36 columns） |
| 407INF | 發票 + 應付格式（30 columns） |
| Batch Name | 406 格式的批次代號 |
| Agent Name | 407 格式的代理人 (預設: TW1411) |

---

**準備好了嗎？** 打開瀏覽器開始轉換吧！ 🎉

http://localhost:8000/invoice_format_converter.html
