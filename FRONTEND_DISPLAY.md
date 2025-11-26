# 前端顯示效果 - OCR 結果層 (Layer 2)

## 📱 整體布局

```
┌─────────────────────────────────────────────────────────┐
│ ✅ OCR 辨識完成                    信賴度: 95%           │
│ 文件已成功辨識並結構化                                    │
├─────────────────────────────────────────────────────────┤
│ 💡 請仔細檢查辨識結果，如果資料有誤，可點「重新辨識」      │
├─────────────────────────────────────────────────────────┤
│                                                          │
│        📊 OCR 辨識資料 (Data Grid)                      │
│                                                          │
├─────────────────────────────────────────────────────────┤
│ 📋 JSON 回應  [精簡版 (推薦)] [原始版]                  │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ { ... JSON 結構化資料 ... }                         │ │
│ └─────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────┤
│  [取消]  [🔄 重新辨識]  [✓ 確認並繼續]                 │
└─────────────────────────────────────────────────────────┘
```

---

## 📊 Data Grid (OCR 辨識資料顯示)

### 發票文件顯示示例

```
┌────────────────────┬──────────────────────────────────┐
│ 發票號碼          │ 2020066251106A                   │
├────────────────────┼──────────────────────────────────┤
│ 發票日期          │ 2025/11/6                        │
├────────────────────┼──────────────────────────────────┤
│ 客戶名稱          │ 威健实业股份有限公司              │
├────────────────────┼──────────────────────────────────┤
│ 總計              │ 212825.81                        │
├────────────────────┼──────────────────────────────────┤
│ 幣別              │ USD                              │
├────────────────────┼──────────────────────────────────┤
│ 狀態              │ ✅ 已辨識                         │
├────────────────────┼──────────────────────────────────┤
│ 辨識信賴度        │ 95%                              │
└────────────────────┴──────────────────────────────────┘
```

**HTML 結構:**
```html
<div class="ocr-data-card">
    <div class="data-grid" id="ocrDataGrid">
        <div class="data-item">
            <div class="data-label">發票號碼</div>
            <div class="data-value">2020066251106A</div>
        </div>
        <div class="data-item">
            <div class="data-label">發票日期</div>
            <div class="data-value">2025/11/6</div>
        </div>
        <!-- ... 其他項目 ... -->
    </div>
</div>
```

---

## 🔄 JSON 檢視器 (JSON Viewer)

### 檢視器頭部 (Header)

```
┌─────────────────────────────────────────────────────────┐
│ 📋 JSON 回應                                             │
│ [精簡版 (推薦)] [原始版]                         [展開/收起]│
└─────────────────────────────────────────────────────────┘
```

### 標籤頁切換功能

#### 🟢 精簡版 (推薦) - 預設顯示

**顯示內容**: ~7.8 KB 結構化資料

```json
{
  "meta": {
    "documentType": "invoice",
    "confidence": 0.92,
    "pageCount": 1
  },
  "extracted": {
    "basic": {
      "invoiceNo": "2020066251106A",
      "date": "2025/11/6",
      "buyer": "威健实业股份有限公司",
      "totalAmount": 212825.81,
      "currency": "USD"
    },
    "supplier": {
      "seller": "GAOSHENGDA TECHNOLOGY HONGKONG CO., LIMITED",
      "sellerAddress": "B/F BLDG 22R 3 SCIENCE PARK FAST AVENUE HONG KONG SCIENCE PARK PAK SHEK KOK SHA TIN HONG KONG",
      "contact": "(0755)82943322-345"
    },
    "terms": {
      "tradeTerm": "FOB CHINA",
      "origin": "中国/No Brand"
    },
    "items": [
      {
        "lineNo": 1,
        "quantity": 9720,
        "unit": "PCS",
        "itemNo": "WL6WR1510",
        "description": "WLGWR1510-DAIKIN",
        "unitPrice": 3.2208,
        "amount": 31306.18,
        "poNo": "750190684"
      },
      {
        "lineNo": 2,
        "quantity": 2124,
        "unit": "PCS",
        "itemNo": "WCT2GM2511",
        "description": "WCT2GM2511-TPV",
        "unitPrice": 2.3461,
        "amount": 4983.12,
        "poNo": "750189359"
      },
      // ... 更多 9 筆項目 ...
    ]
  },
  "fullText": "GAOSHENGDA TECHNOLOGY HONGKONG CO., LIMITED\nB/F BLDG 22R..."
}
```

**優點:**
- ✅ 體積小 (7.8 KB)
- ✅ 結構清晰
- ✅ 易於存儲和傳輸
- ✅ 包含所有重要欄位

---

#### 🔵 原始版 - Document Intelligence API 回應

**顯示內容**: ~7.4 MB 完整原始回應

```json
{
  "status": "succeeded",
  "createdDateTime": "2025-11-25T07:27:22Z",
  "lastUpdatedDateTime": "2025-11-25T07:27:24Z",
  "analyzeResult": {
    "apiVersion": "2024-02-29-preview",
    "modelId": "prebuilt-read",
    "stringIndexType": "textElements",
    "content": "GAOSHENGDA TECHNOLOGY HONGKONG CO., LIMITED\nB/F BLDG 22R 3 SCIENCE PARK...",
    "pages": [
      {
        "pageNumber": 1,
        "angle": 0,
        "width": 8.2639,
        "height": 11.6806,
        "unit": "inch",
        "words": [
          {
            "content": "GAOSHENGDA",
            "polygon": [...],
            "confidence": 0.995,
            "span": { "offset": 0, "length": 10 }
          },
          // ... 數百個 words 對象 ...
        ],
        "lines": [
          // ... 所有行信息 ...
        ],
        "spans": [...]
      }
    ],
    "paragraphs": [
      // ... 81 個段落 ...
    ],
    "styles": [...]
  }
}
```

**用途:**
- 📍 完整保留原始數據
- 🔍 用於調試和驗證
- 📦 支持將來的增強提取

---

## 🎨 CSS 樣式效果

### Data Grid 樣式

```css
.data-grid {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
}

.data-item {
  border: 1px solid #e0e0e0;
  border-radius: 4px;
  padding: 12px;
  background: #f9f9f9;
}

.data-label {
  font-size: 12px;
  color: #666;
  font-weight: 500;
  margin-bottom: 4px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.data-value {
  font-size: 14px;
  color: #333;
  font-weight: 600;
  word-break: break-word;
}
```

### JSON Tab 樣式

```css
.json-view-tabs {
  display: flex;
  gap: 8px;
}

.json-tab {
  padding: 6px 12px;
  border: 1px solid #ddd;
  background: #fff;
  cursor: pointer;
  border-radius: 3px;
  font-size: 12px;
  transition: all 0.3s;
}

.json-tab.active {
  background: #4CAF50;
  color: white;
  border-color: #4CAF50;
}

.json-tab:hover {
  background: #f0f0f0;
}

.json-tab.active:hover {
  background: #45a049;
}
```

---

## 🔌 JavaScript 互動邏輯

### switchJSONView() 函數

```javascript
function switchJSONView(viewType) {
    // 更新標籤頁狀態
    const tabs = document.querySelectorAll('.json-tab');
    tabs.forEach(tab => tab.classList.remove('active'));
    event.target.classList.add('active');
    
    // 切換 JSON 內容
    const jsonContent = document.getElementById('jsonContent');
    if (viewType === 'simplified') {
        jsonContent.textContent = JSON.stringify(ocrData._simplified, null, 2);
    } else {
        jsonContent.textContent = JSON.stringify(ocrData._raw, null, 2);
    }
    
    // 自動展開檢視器
    const container = document.getElementById('jsonViewerContainer');
    container.classList.remove('hidden');
}
```

### showOCRResults() 函數

```javascript
function showOCRResults() {
    updateStep(2);
    showLayer('layer2');
    
    // 生成 OCR 資料 (排除內部欄位)
    const dataGrid = document.getElementById('ocrDataGrid');
    dataGrid.innerHTML = '';
    
    for (const [label, value] of Object.entries(ocrData)) {
        if (label.startsWith('_')) continue; // 跳過內部欄位 (_raw, _simplified, _allText)
        
        const item = document.createElement('div');
        item.className = 'data-item';
        item.innerHTML = `
            <div class="data-label">${label}</div>
            <div class="data-value">${value}</div>
        `;
        dataGrid.appendChild(item);
    }
    
    // 預設顯示精簡版 JSON
    if (ocrData._simplified) {
        const jsonContent = document.getElementById('jsonContent');
        jsonContent.textContent = JSON.stringify(ocrData._simplified, null, 2);
    }
    
    // 在 console 顯示完整信息
    console.log('✅ 方案 A 改進版 - 詳細提取資料');
    console.log('精簡版 (7.8 KB):', ocrData._simplified);
    console.log('原始版 (7.4 MB):', ocrData._raw);
    console.log('完整文本:', ocrData._allText);
}
```

---

## 🎬 用戶交互流程

### 步驟 1: 上傳 PDF → 開始辨識

```
[選擇發票/訂單] → [上傳 PDF] → [開始辨識] → 進度條顯示
```

### 步驟 2: 顯示 OCR 結果

```
前端顯示:
  1. Data Grid: 顯示基本欄位 (發票號、日期、金額等)
  2. JSON 檢視器: 可切換精簡/原始版本
  3. 信賴度徽章: 顯示辨識信心度 (95%)

用戶操作:
  - [取消]: 返回開始層
  - [�� 重新辨識]: 重新上傳同一文件
  - [✓ 確認並繼續]: 進入匯出格式選擇層
```

### 步驟 3: 選擇匯出格式

```
點擊 [✓ 確認並繼續] → Layer 3 顯示:
  - 已辨識資料摘要 (Data Summary)
  - 匯出格式選項 (格式卡片)
    ├─ 406 格式 (XLSX)
    ├─ 407 格式 (CSV)
    └─ 訂單則顯示 CO.csv, SO.csv 等
```

---

## 💾 localStorage 存儲結構

### ocrData 物件結構

```javascript
ocrData = {
  // 公開欄位 (顯示在 Data Grid)
  "發票號碼": "2020066251106A",
  "發票日期": "2025/11/6",
  "客戶名稱": "威健实业股份有限公司",
  "總計": "212825.81",
  "幣別": "USD",
  "狀態": "✅ 已辨識",
  "辨識信賴度": "95%",
  
  // 內部欄位 (不顯示，但保存以供後續使用)
  "_raw": { /* 完整 DI API 回應 */ },
  "_simplified": { /* 方案 A 改進版結構 */ },
  "_allText": "GAOSHENGDA TECHNOLOGY..." // 完整文本
}
```

---

## 📈 控制台 (Console) 輸出

當 OCR 完成時，開發者工具 Console 顯示:

```
✅ 方案 A 改進版 - 詳細提取資料
精簡版 (7.8 KB): {
  meta: { ... },
  extracted: { ... },
  fullText: "..."
}
原始版 (7.4 MB): {
  status: "succeeded",
  analyzeResult: { ... }
}
完整文本: "GAOSHENGDA TECHNOLOGY..."
```

---

## 🎯 用戶看到的最終效果

### 第 2 層 - OCR 結果層顯示

```
┌──────────────────────────────────────────────────────────┐
│ ✅ OCR 辨識完成                     信賴度: 95%          │
│ 文件已成功辨識並結構化                                    │
├──────────────────────────────────────────────────────────┤
│ 💡 請仔細檢查辨識結果，如果資料有誤，可點「重新辨識」    │
├──────────────────────────────────────────────────────────┤
│                                                          │
│                 📊 OCR 辨識資料                          │
│  ┌───────────────┬─────────────────────────────┐       │
│  │ 發票號碼     │ 2020066251106A               │       │
│  ├───────────────┼─────────────────────────────┤       │
│  │ 發票日期     │ 2025/11/6                   │       │
│  ├───────────────┼─────────────────────────────┤       │
│  │ 客戶名稱     │ 威健实业股份有限公司         │       │
│  ├───────────────┼─────────────────────────────┤       │
│  │ 總計        │ 212825.81                    │       │
│  ├───────────────┼─────────────────────────────┤       │
│  │ 幣別        │ USD                          │       │
│  ├───────────────┼─────────────────────────────┤       │
│  │ 狀態        │ ✅ 已辨識                    │       │
│  ├───────────────┼─────────────────────────────┤       │
│  │ 辨識信賴度   │ 95%                         │       │
│  └───────────────┴─────────────────────────────┘       │
│                                                          │
│ 📋 JSON 回應                                             │
│ [精簡版 (推薦)] [原始版]                         [展開/收起]│
│ ┌──────────────────────────────────────────────────┐   │
│ │ {                                               │   │
│ │   "meta": {                                     │   │
│ │     "documentType": "invoice",                  │   │
│ │     "confidence": 0.92,                         │   │
│ │     "pageCount": 1                              │   │
│ │   },                                            │   │
│ │   "extracted": {                                │   │
│ │     "basic": {                                  │   │
│ │       "invoiceNo": "2020066251106A",            │   │
│ │       "date": "2025/11/6",                      │   │
│ │       "buyer": "威健实业股份有限公司",           │   │
│ │       "totalAmount": 212825.81,                 │   │
│ │       "currency": "USD"                         │   │
│ │     },                                          │   │
│ │     "supplier": { ... },                        │   │
│ │     "terms": { ... },                           │   │
│ │     "items": [ ... ]  // 11 筆項目              │   │
│ │   },                                            │   │
│ │   "fullText": "..."                             │   │
│ │ }                                               │   │
│ └──────────────────────────────────────────────────┘   │
│                                                          │
│        [取消]  [🔄 重新辨識]  [✓ 確認並繼續]            │
└──────────────────────────────────────────────────────────┘
```

