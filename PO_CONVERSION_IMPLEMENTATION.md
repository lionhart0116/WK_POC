# 採購單格式轉換系統實作說明

## 📋 概述

本系統實作了採購單 OCR 辨識結果轉換為三種 Excel 格式的功能：
1. **CO** (Customer Order - 客戶訂單)
2. **SO** (Sales Order - 銷售訂單)
3. **兩階段** (Two-Stage Transfer - 倉庫調撥)

## 🗂️ 檔案結構

### 後端檔案 (C#)

#### 1. `POExcelConverter.cs`
- **功能**: 採購單 JSON 轉換為 Excel 的核心邏輯
- **主要方法**:
  - `ConvertToCO()` - 轉換為 CO 格式
  - `ConvertToSO()` - 轉換為 SO 格式
  - `ConvertToTwoStage()` - 轉換為兩階段格式
  - `ConvertToExcel()` - 根據格式自動選擇轉換方法

#### 2. `ConvertPOToExcel.cs`
- **功能**: Azure Function HTTP 端點
- **路由**: `POST /api/convert-po-to-excel`
- **請求格式**:
  ```json
  {
    "poJson": "{...}",      // 採購單 JSON 字串
    "format": "CO",         // CO / SO / TWOSTAGE
    "paramValue": "AUTO"    // 可選參數
  }
  ```

#### 3. `UploadOcr_SDK.cs` (已修改)
- **修改內容**:
  1. 修正地址欄位返回 "Azure.AI.DocumentIntelligence.AddressValue" 的問題
  2. 新增採購單號提取 fallback 邏輯
  3. 新增文件類型檢測 (採購單 vs 發票)
  4. 新增 `ExtractPurchaseOrderNumber()` 函數

### 前端檔案 (HTML/JavaScript)

#### 1. `po_format_converter.html` (已修改)
- **修改內容**:
  1. 格式選擇改為三種: CO / SO / 兩階段
  2. 更新參數說明
  3. 更新 `selectFormat()` 函數
  4. 更新 `downloadExcel()` 函數以支援新的 API
  5. 優化採購單號、送貨地址、聯絡人、付款條件的提取邏輯

## 📊 三種格式對照表

### CO 格式 (15 欄位)
| 欄位 | 說明 | 來源 |
|------|------|------|
| Co_Number | 客戶訂單號 | 參數或自動生成 |
| Cust_Po_Number | 客戶採購單號 | poNo |
| Cust_Po_Line_num | 行號 | 自動編號 |
| 客戶料號 | 料號 | itemNo |
| 廠商料號 | 料號 | itemNo |
| Co_Qty | 數量 | quantity |
| 客戶品名 | 品名 | description |
| Unit_Selling_Price | 單價 | unitPrice |
| Cust_Request_Date | 交期 | deliveryDate |
| Cust_Order_Date | 訂單日期 | poDate |
| End_Customer | 最終客戶 | buyer |

### SO 格式 (34 欄位)
| 欄位 | 說明 | 來源 |
|------|------|------|
| UPLOAD NO | 上傳編號 | 參數或自動生成 |
| LINE | 行號 | 自動編號 |
| CUSTOMER NO. | 客戶代號 | 參數 |
| PO NO. | 採購單號 | poNo |
| DATE | 日期 | poDate |
| SHIP TO | 送貨地址 | deliveryAddress |
| SHIP TO ATTN | 聯絡人 | contact |
| BILL TO | 帳單地址 | buyer |
| CURRENCYY | 幣別 | currency |
| P/N | 料號 | itemNo |
| ITEM | 品名 | description |
| QTY | 數量 | quantity |
| PRICE | 單價 | unitPrice |
| PAYMENT TERM | 付款條件 | paymentTerm |

### 兩階段格式 (33 欄位)
| 欄位 | 說明 | 來源 |
|------|------|------|
| 訂單名 | 訂單名稱 | 參數或自動生成 |
| From Org | 來源組織 | 固定 "T02" |
| To Org | 目的組織 | 固定 "T02" |
| 類別 | 單據類別 | 固定 "預約單" |
| Transaction Type | 交易類型 | 固定 "WK_預約-HUB" |
| 發出客戶代號 | 客戶 | buyer |
| 客戶料號 | 採購單號 | poNo |
| From Item | 料號 | itemNo |
| Quantity | 數量 | quantity |

## 🔧 部署步驟

### 1. 後端部署
```bash
# 1. 確認所有 C# 檔案已更新
# - POExcelConverter.cs (新增)
# - ConvertPOToExcel.cs (新增)
# - UploadOcr_SDK.cs (已修改)

# 2. 部署到 Azure
cd /path/to/CODE
func azure functionapp publish wk-pdf-ocr
```

### 2. 前端部署
```bash
# po_format_converter.html 已更新
# 直接上傳到靜態網站託管或測試使用
```

## 🧪 測試流程

### 1. 上傳採購單 OCR
1. 開啟 `index.html`
2. 上傳採購單 PDF
3. OCR 辨識完成後,選擇「採購單」類型
4. 點擊「確認並繼續」

### 2. 選擇格式並下載
1. 自動跳轉到 `po_format_converter.html`
2. 檢視解析結果
3. 選擇輸出格式 (CO / SO / 兩階段)
4. 輸入參數 (可選,預設 AUTO)
5. 點擊「下載 Excel」

## 📝 JSON 資料結構

### 輸入格式 (採購單 JSON)
```json
{
  "poNo": "20221110005",
  "poDate": "2022-11-10",
  "buyer": "誠欽科技股份有限公司",
  "seller": "威健實業股份有限公司",
  "sellerAddress": "台北市內湖路一段308號11樓",
  "deliveryAddress": "台北市瑞光路106號4樓之1",
  "contact": "劉亦鈞Frank 0916-077-155",
  "paymentTerm": "01 月結30天",
  "totalAmount": 3241.35,
  "currency": "USD",
  "items": [
    {
      "lineNo": 1,
      "itemNo": "D01-01-SMM6108IQ0-OQ",
      "description": "IC (Morse Micro, MM6108IQ-TR)\nSoC, IEEE 802.11ah WiFi, QFN-48L_6x6mm",
      "quantity": 490,
      "unit": "PCS",
      "unitPrice": 6.3,
      "amount": 3087,
      "deliveryDate": "2023-01-06"
    }
  ]
}
```

## 🐛 已修正的問題

### 後端修正
1. ✅ 地址欄位返回 "Azure.AI.DocumentIntelligence.AddressValue"
   - 修改: 使用 `field.Content` 而非 `field.ValueAddress.ToString()`

2. ✅ 採購單號無法提取
   - 新增: `ExtractPurchaseOrderNumber()` 函數
   - 支援: `採購單號:XXXXX` 格式

3. ✅ 買方/賣方角色互換
   - 新增: 文件類型檢測邏輯
   - 區分: 採購單 vs 發票的角色映射

### 前端修正
1. ✅ 送貨地址跨行提取
2. ✅ 聯絡人資訊提取
3. ✅ 付款條件跨列提取
4. ✅ 買方公司名稱識別

## 📌 注意事項

### 欄位映射
- 部分欄位使用固定值 (如 SO 的 ORDER_TYPE, 兩階段的 From/To Org)
- 部分欄位需要外部參數 (如 SO 的 CUSTOMER_NO)
- 建議根據實際業務需求調整固定值

### 編碼問題
- 範例 CSV 檔案有編碼問題 (顯示為亂碼)
- 實際生成的 Excel 使用 UTF-8 編碼,不會有亂碼

### 自動編號
- `AUTO` 參數會自動生成時間戳記編號
- 格式: `PO{yyMMddHHmmss}` (CO) / `tw{yyMMddHHmmss}` (SO) / `TW{yyMMddHHmmss}` (兩階段)

## 🚀 未來優化建議

1. **欄位對照表管理**
   - 建立資料庫或設定檔管理欄位映射規則
   - 支援使用者自訂欄位對照

2. **批次處理**
   - 支援一次上傳多張採購單
   - 合併輸出到單一 Excel 檔案

3. **範本管理**
   - 支援匯入自訂 Excel 範本
   - 動態調整欄位位置和格式

4. **資料驗證**
   - 加強必填欄位檢查
   - 數值格式驗證
   - 日期格式統一

5. **錯誤處理**
   - 詳細的錯誤訊息
   - 部分失敗時的降級處理
   - 錯誤日誌記錄
