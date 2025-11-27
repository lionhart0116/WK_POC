using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.AI.DocumentIntelligence;
using Azure;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace wk.pdf.ocr;

/// <summary>
/// OCR Function using Azure.AI.DocumentIntelligence official SDK
/// This version uses the official NuGet package instead of direct HTTP calls
/// </summary>
public class UploadOcrSDK
{
    private readonly ILogger<UploadOcrSDK> _logger;
    
    public UploadOcrSDK(ILogger<UploadOcrSDK> logger)
    {
        _logger = logger;
    }

    [Function("upload-ocr-sdk")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "upload-ocr-sdk")] HttpRequest req)
    {
        _logger.LogInformation("OCR function invoked using official SDK");

        try
        {
            // Validate and read uploaded file
            if (!req.HasFormContentType || req.Form.Files.Count == 0)
            {
                return new BadRequestObjectResult(new 
                { 
                    error = "No file uploaded. Include a file in form-data (field name: file)." 
                });
            }

            var file = req.Form.Files[0];
            var fileName = file.FileName?.ToLowerInvariant() ?? string.Empty;

            // Validate file type
            if (!IsValidFileType(fileName))
            {
                return new BadRequestObjectResult(new 
                { 
                    error = "Uploaded file must be a PDF or TIFF." 
                });
            }

            // Validate file size
            var maxBytes = GetMaxFileBytes();
            if (file.Length > maxBytes)
            {
                _logger.LogWarning("File {fileName} too large: {size} bytes (max {max})", 
                    file.FileName, file.Length, maxBytes);
                return new ObjectResult(new 
                { 
                    error = $"File too large. Maximum size: {maxBytes / 1_000_000}MB" 
                }) 
                { StatusCode = 413 };
            }

            // Read file to memory
            await using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            _logger.LogInformation("Processing file: {fileName}, size: {size} bytes", 
                file.FileName, file.Length);

            // Initialize Document Intelligence client
            var client = InitializeClient();
            if (client == null)
            {
                return new ObjectResult(new 
                { 
                    error = "Document Intelligence not configured" 
                }) 
                { StatusCode = 500 };
            }

            // Analyze document
            var analyzeContent = BinaryData.FromBytes(memoryStream.ToArray());

            _logger.LogInformation("Calling Document Intelligence API with prebuilt-invoice model");

            var operation = await client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                "prebuilt-invoice",
                analyzeContent);

            if (!operation.HasValue)
            {
                return new ObjectResult(new 
                { 
                    error = "Document Intelligence operation failed" 
                }) 
                { StatusCode = 500 };
            }

            var analyzeResult = operation.Value;
            
            _logger.LogInformation("Document Intelligence analysis completed. Pages: {pageCount}", 
                analyzeResult.Pages?.Count ?? 0);

            // Structure the OCR result
            var structuredResult = StructureOCRResult(analyzeResult, file.FileName);

            return new OkObjectResult(structuredResult);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Document Intelligence API error: {status} {message}", 
                ex.Status, ex.Message);
            return new ObjectResult(new 
            { 
                error = "Document Intelligence API error", 
                details = ex.Message,
                status = ex.Status 
            }) 
            { StatusCode = ex.Status };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during OCR processing");
            return new ObjectResult(new 
            { 
                error = "Unexpected error during processing", 
                details = ex.Message 
            }) 
            { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Initialize Document Intelligence client
    /// </summary>
    private DocumentIntelligenceClient? InitializeClient()
    {
        var endpoint = Environment.GetEnvironmentVariable("AZ_DOCUMENT_INTELLIGENCE_ENDPOINT");
        var key = Environment.GetEnvironmentVariable("AZ_DOCUMENT_INTELLIGENCE_KEY");

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
        {
            _logger.LogError("Document Intelligence endpoint or key not configured");
            return null;
        }

        try
        {
            return new DocumentIntelligenceClient(new Uri(endpoint), new AzureKeyCredential(key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Document Intelligence client");
            return null;
        }
    }

    /// <summary>
    /// Validate file type
    /// </summary>
    private bool IsValidFileType(string fileName)
    {
        return fileName.EndsWith(".pdf") || fileName.EndsWith(".tif") || fileName.EndsWith(".tiff");
    }

    /// <summary>
    /// Get maximum file size in bytes
    /// </summary>
    private int GetMaxFileBytes()
    {
        var envValue = Environment.GetEnvironmentVariable("AZ_OCR_MAX_FILE_BYTES");
        if (int.TryParse(envValue, out var maxBytes))
        {
            return maxBytes;
        }
        return 20_000_000; // Default: 20MB
    }

    /// <summary>
    /// Structure OCR result into Key-Value format
    /// </summary>
    private object StructureOCRResult(AnalyzeResult analyzeResult, string? fileName)
    {
        var fullText = string.Join("\n", 
            analyzeResult.Pages?.SelectMany(p => p.Lines ?? Enumerable.Empty<DocumentLine>())
                .Select(l => l.Content) ?? Enumerable.Empty<string>());

        // 優先使用 prebuilt-invoice 模型提取的結構化欄位
        var extracted = ExtractInvoiceFieldsFromModel(analyzeResult, fullText);

        // 計算整體信賴度
        var confidence = CalculateOverallConfidence(analyzeResult);

        return new
        {
            meta = new
            {
                apiVersion = "2024-02-29-preview",
                model = "prebuilt-invoice",
                pageCount = analyzeResult.Pages?.Count ?? 0,
                timestamp = DateTime.UtcNow.ToString("O"),
                fileName = fileName,
                confidence = confidence
            },
            extracted = extracted,
            fullText = fullText,
            raw = SerializeAnalyzeResult(analyzeResult)
        };
    }

    /// <summary>
    /// Calculate overall confidence score from the analysis result
    /// </summary>
    private object CalculateOverallConfidence(AnalyzeResult analyzeResult)
    {
        var confidenceInfo = new Dictionary<string, object>();
        
        // 文件層級信賴度
        if (analyzeResult.Documents != null && analyzeResult.Documents.Count > 0)
        {
            var doc = analyzeResult.Documents[0];
            confidenceInfo["document"] = Math.Round(doc.Confidence * 100, 2);
            
            // 欄位層級信賴度
            if (doc.Fields != null)
            {
                var fieldConfidences = new Dictionary<string, double>();
                foreach (var field in doc.Fields)
                {
                    if (field.Value.Confidence.HasValue)
                    {
                        fieldConfidences[field.Key] = Math.Round(field.Value.Confidence.Value * 100, 2);
                    }
                }
                if (fieldConfidences.Count > 0)
                {
                    confidenceInfo["fields"] = fieldConfidences;
                    confidenceInfo["averageFieldConfidence"] = Math.Round(fieldConfidences.Values.Average(), 2);
                }
            }
        }

        // 頁面層級平均信賴度（基於文字行）
        if (analyzeResult.Pages != null)
        {
            var pageConfidences = new List<double>();
            foreach (var page in analyzeResult.Pages)
            {
                if (page.Words != null && page.Words.Count > 0)
                {
                    var avgWordConfidence = page.Words.Average(w => w.Confidence);
                    pageConfidences.Add(Math.Round(avgWordConfidence * 100, 2));
                }
            }
            if (pageConfidences.Count > 0)
            {
                confidenceInfo["pages"] = pageConfidences;
                confidenceInfo["averagePageConfidence"] = Math.Round(pageConfidences.Average(), 2);
            }
        }

        // 計算整體信賴度（優先使用文件信賴度，其次是平均欄位信賴度）
        if (confidenceInfo.ContainsKey("document"))
        {
            confidenceInfo["overall"] = confidenceInfo["document"];
        }
        else if (confidenceInfo.ContainsKey("averageFieldConfidence"))
        {
            confidenceInfo["overall"] = confidenceInfo["averageFieldConfidence"];
        }
        else if (confidenceInfo.ContainsKey("averagePageConfidence"))
        {
            confidenceInfo["overall"] = confidenceInfo["averagePageConfidence"];
        }
        else
        {
            confidenceInfo["overall"] = 0;
        }

        return confidenceInfo;
    }

    /// <summary>
    /// Extract invoice fields from prebuilt-invoice model result
    /// </summary>
    private Dictionary<string, object?> ExtractInvoiceFieldsFromModel(AnalyzeResult analyzeResult, string fullText)
    {
        var fields = new Dictionary<string, object?>();

        // 檢測文件類型（採購單 vs 發票）
        bool isPurchaseOrder = fullText.Contains("採購單號") || fullText.Contains("Purchase Order") || fullText.Contains("P.O.");

        // 從 prebuilt-invoice 模型的 Documents 提取結構化欄位
        if (analyzeResult.Documents != null && analyzeResult.Documents.Count > 0)
        {
            var invoice = analyzeResult.Documents[0];
            var docFields = invoice.Fields;

            if (docFields != null)
            {
                // 發票號碼
                fields["invoiceNo"] = GetFieldValue(docFields, "InvoiceId");

                // 日期
                fields["date"] = GetFieldValue(docFields, "InvoiceDate") ?? GetFieldValue(docFields, "DueDate");

                // 採購單的角色與發票相反
                if (isPurchaseOrder)
                {
                    // 採購單：VendorName = 供應商(seller), CustomerName = 買方(buyer)
                    fields["seller"] = GetFieldValue(docFields, "VendorName");
                    fields["sellerAddress"] = GetFieldValue(docFields, "VendorAddress");
                    fields["sellerTaxId"] = GetFieldValue(docFields, "VendorTaxId");

                    fields["buyer"] = GetFieldValue(docFields, "CustomerName");
                    fields["buyerAddress"] = GetFieldValue(docFields, "CustomerAddress");
                    fields["buyerTaxId"] = GetFieldValue(docFields, "CustomerTaxId");
                }
                else
                {
                    // 發票：VendorName = 開票方(seller), CustomerName = 收票方(buyer)
                    fields["seller"] = GetFieldValue(docFields, "VendorName");
                    fields["sellerAddress"] = GetFieldValue(docFields, "VendorAddress");
                    fields["sellerTaxId"] = GetFieldValue(docFields, "VendorTaxId");

                    fields["buyer"] = GetFieldValue(docFields, "CustomerName");
                    fields["buyerAddress"] = GetFieldValue(docFields, "CustomerAddress");
                    fields["buyerTaxId"] = GetFieldValue(docFields, "CustomerTaxId");
                }
                
                // 金額資訊
                fields["subTotal"] = GetFieldValue(docFields, "SubTotal");
                fields["totalTax"] = GetFieldValue(docFields, "TotalTax");
                fields["totalAmount"] = GetFieldValue(docFields, "InvoiceTotal") ?? GetFieldValue(docFields, "AmountDue");
                
                // 幣別
                fields["currency"] = GetFieldValue(docFields, "CurrencyCode") ?? ExtractCurrency(fullText);
                
                // 付款條款
                fields["paymentTerm"] = GetFieldValue(docFields, "PaymentTerm");
                
                // PO 編號
                fields["purchaseOrder"] = GetFieldValue(docFields, "PurchaseOrder");
                
                // 提取品項
                fields["items"] = ExtractItemsFromModel(docFields);
            }
        }

        // 如果 prebuilt-invoice 沒有提取到某些欄位，使用 fallback 方法
        if (fields["invoiceNo"] == null) fields["invoiceNo"] = ExtractInvoiceNumber(fullText);
        if (fields["purchaseOrder"] == null) fields["purchaseOrder"] = ExtractPurchaseOrderNumber(fullText);
        if (fields["date"] == null) fields["date"] = ExtractDate(fullText);
        if (fields["seller"] == null) fields["seller"] = ExtractSeller(fullText, analyzeResult);
        if (fields["buyer"] == null) fields["buyer"] = ExtractBuyer(fullText);
        if (fields["totalAmount"] == null) fields["totalAmount"] = ExtractTotalAmount(fullText);
        
        // 額外欄位（prebuilt-invoice 可能沒有的）
        fields["contact"] = ExtractContact(fullText);
        fields["tradeTerm"] = ExtractTradeTerm(fullText);
        fields["origin"] = ExtractOrigin(fullText);
        fields["remarks"] = ExtractRemarks(fullText);

        // 如果沒有從模型提取到品項，使用 fallback 方法
        if (fields["items"] == null || (fields["items"] is List<Dictionary<string, object?>> list && list.Count == 0))
        {
            fields["items"] = ExtractItems(analyzeResult, fullText);
        }

        // ✅ 將 purchaseOrder 拆分並分配給各個 item
        AssignPurchaseOrdersToItems(fields);

        return fields;
    }

    /// <summary>
    /// 將發票層級的 purchaseOrder（多個用換行分隔）拆分並分配給各個 item
    /// </summary>
    private void AssignPurchaseOrdersToItems(Dictionary<string, object?> fields)
    {
        // 獲取 purchaseOrder 字串
        var purchaseOrderStr = fields["purchaseOrder"]?.ToString();
        if (string.IsNullOrEmpty(purchaseOrderStr))
            return;

        // 獲取 items
        if (fields["items"] is not List<Dictionary<string, object?>> items || items.Count == 0)
            return;

        // 拆分 purchaseOrder（支援換行符和逗號分隔）
        var poList = purchaseOrderStr
            .Split(new[] { '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(po => po.Trim())
            .Where(po => !string.IsNullOrEmpty(po))
            .ToList();

        _logger.LogInformation($"📋 解析 PurchaseOrder: 共 {poList.Count} 個 PO, {items.Count} 個 items");

        // 如果只有一個 PO，分配給所有 item
        if (poList.Count == 1)
        {
            foreach (var item in items)
            {
                item["customerPO"] = poList[0];
            }
            _logger.LogInformation($"✅ 單一 PO '{poList[0]}' 已分配給所有 {items.Count} 個 items");
        }
        // 如果 PO 數量與 item 數量相同，一對一分配
        else if (poList.Count == items.Count)
        {
            for (int i = 0; i < items.Count; i++)
            {
                items[i]["customerPO"] = poList[i];
            }
            _logger.LogInformation($"✅ {poList.Count} 個 PO 已一對一分配給 {items.Count} 個 items");
        }
        // 如果 PO 數量與 item 數量不同，盡量分配
        else
        {
            for (int i = 0; i < items.Count; i++)
            {
                // 循環使用 PO，或者只用有的
                items[i]["customerPO"] = i < poList.Count ? poList[i] : poList.LastOrDefault();
            }
            _logger.LogWarning($"⚠️ PO 數量 ({poList.Count}) 與 item 數量 ({items.Count}) 不匹配，已盡量分配");
        }

        // 清除發票層級的 purchaseOrder（因為已經分配到各個 item）
        fields["purchaseOrder"] = null;
    }

    /// <summary>
    /// Get field value from document fields
    /// </summary>
    private object? GetFieldValue(IReadOnlyDictionary<string, DocumentField> fields, string fieldName)
    {
        if (!fields.TryGetValue(fieldName, out var field))
            return null;

        // 使用 if-else 而非 switch，因為 DocumentFieldType 是 struct
        if (field.FieldType == DocumentFieldType.String)
            return field.ValueString;
        if (field.FieldType == DocumentFieldType.Date)
            return field.ValueDate?.ToString("yyyy-MM-dd");
        if (field.FieldType == DocumentFieldType.Time)
            return field.ValueTime?.ToString();
        if (field.FieldType == DocumentFieldType.PhoneNumber)
            return field.ValuePhoneNumber;
        if (field.FieldType == DocumentFieldType.Double)
            return field.ValueDouble;
        if (field.FieldType == DocumentFieldType.Int64)
            return field.ValueInt64;
        if (field.FieldType == DocumentFieldType.Currency)
            return field.ValueCurrency?.Amount;
        if (field.FieldType == DocumentFieldType.Address)
            return field.Content; // 使用 Content 而不是 ToString()，避免返回類型名稱
        if (field.FieldType == DocumentFieldType.CountryRegion)
            return field.ValueCountryRegion;
        
        return field.Content;
    }

    /// <summary>
    /// Extract items from prebuilt-invoice model
    /// </summary>
    private List<Dictionary<string, object?>> ExtractItemsFromModel(IReadOnlyDictionary<string, DocumentField> docFields)
    {
        var items = new List<Dictionary<string, object?>>();

        if (!docFields.TryGetValue("Items", out var itemsField))
            return items;

        if (itemsField.FieldType != DocumentFieldType.List || itemsField.ValueList == null)
            return items;

        var lineNo = 1;
        foreach (var itemField in itemsField.ValueList)
        {
            if (itemField.FieldType != DocumentFieldType.Dictionary || itemField.ValueDictionary == null)
                continue;

            var itemDict = itemField.ValueDictionary;
            var item = new Dictionary<string, object?>
            {
                ["lineNo"] = lineNo++
            };

            // 品項描述
            if (itemDict.TryGetValue("Description", out var desc))
                item["description"] = desc.Content ?? desc.ValueString;

            // 品項編號/產品代碼
            if (itemDict.TryGetValue("ProductCode", out var code))
                item["itemNo"] = code.Content ?? code.ValueString;

            // 數量
            if (itemDict.TryGetValue("Quantity", out var qty))
                item["quantity"] = qty.ValueDouble ?? (double?)qty.ValueInt64;

            // 單位
            if (itemDict.TryGetValue("Unit", out var unit))
                item["unit"] = unit.Content ?? unit.ValueString;

            // 單價
            if (itemDict.TryGetValue("UnitPrice", out var unitPrice))
                item["unitPrice"] = unitPrice.ValueCurrency?.Amount ?? unitPrice.ValueDouble;

            // 金額
            if (itemDict.TryGetValue("Amount", out var amount))
                item["amount"] = amount.ValueCurrency?.Amount ?? amount.ValueDouble;

            // 稅額
            if (itemDict.TryGetValue("Tax", out var tax))
                item["tax"] = tax.ValueCurrency?.Amount ?? tax.ValueDouble;

            // 日期
            if (itemDict.TryGetValue("Date", out var date))
                item["date"] = date.ValueDate?.ToString("yyyy-MM-dd");

            items.Add(item);
        }

        return items;
    }

    #region Field Extraction Methods

    private string? ExtractInvoiceNumber(string fullText)
    {
        // Pattern 1: 數字+字母 (e.g., 2020066251106A)
        var match = System.Text.RegularExpressions.Regex.Match(fullText, @"(\d{10,14}[A-Z]{1,3})\b");
        if (match.Success) return match.Groups[1].Value;

        // Pattern 2: Invoice No: XXX
        match = System.Text.RegularExpressions.Regex.Match(fullText,
            @"(?:Invoice\s*No|INV\s*No|INVOICE#)[\s:]*([A-Z0-9\-]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value.Trim();

        return null;
    }

    private string? ExtractPurchaseOrderNumber(string fullText)
    {
        // Pattern 1: 採購單號: YYYYMMDDXXX (台灣格式)
        var match = System.Text.RegularExpressions.Regex.Match(fullText,
            @"採購單號[\s：:]*(\d{8,15})");
        if (match.Success) return match.Groups[1].Value.Trim();

        // Pattern 2: PO No: XXX or P.O. No: XXX
        match = System.Text.RegularExpressions.Regex.Match(fullText,
            @"(?:P\.?O\.?\s*No|Purchase\s*Order\s*No)[\s:]*([A-Z0-9\-]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value.Trim();

        return null;
    }

    private string? ExtractDate(string fullText)
    {
        // Pattern 1: YYYY/MM/DD or YYYY-MM-DD
        var match = System.Text.RegularExpressions.Regex.Match(fullText, 
            @"(?:DATE|INVOICE DATE|INVOICE\s*DATE)[\s:]*(\d{4}[-\/]\d{1,2}[-\/]\d{1,2})", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value;

        // Pattern 2: Any date format
        match = System.Text.RegularExpressions.Regex.Match(fullText, @"(\d{4}[-\/]\d{1,2}[-\/]\d{1,2})");
        if (match.Success) return match.Groups[1].Value;

        return null;
    }

    private string? ExtractSeller(string fullText, AnalyzeResult analyzeResult)
    {
        // Get first few lines
        var lines = fullText.Split('\n').Take(5);
        foreach (var line in lines)
        {
            if (line.Length > 5 && line.Length < 100 && 
                System.Text.RegularExpressions.Regex.IsMatch(line, @"[A-Z]{3,}"))
            {
                return line.Trim();
            }
        }
        return null;
    }

    private string? ExtractSellerAddress(string fullText)
    {
        var match = System.Text.RegularExpressions.Regex.Match(fullText, 
            @"(?:B\/F\s+BLDG|ADDRESS|地址)[\s:]*([^\n]+)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private string? ExtractContact(string fullText)
    {
        var match = System.Text.RegularExpressions.Regex.Match(fullText, 
            @"(?:TEL|PHONE|CONTACT)[\s:]*([0-9\-\s\(\)]+)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private string? ExtractBuyer(string fullText)
    {
        var match = System.Text.RegularExpressions.Regex.Match(fullText, 
            @"(?:BUYER|CUSTOMER|SOLD TO|BILL TO)[\s:]*\n?\s*([^\n]+)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private string? ExtractBuyerAddress(string fullText)
    {
        return null; // Implement as needed
    }

    private string? ExtractTradeTerm(string fullText)
    {
        var match = System.Text.RegularExpressions.Regex.Match(fullText, 
            @"(?:TRADE\s*TERM|INCOTERM)[\s:]*([^\n]+)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private string? ExtractOrigin(string fullText)
    {
        var match = System.Text.RegularExpressions.Regex.Match(fullText, 
            @"(?:原产国|ORIGIN|COUNTRY)[\s:]*([^\n]+)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private string? ExtractCurrency(string fullText)
    {
        var currencies = new[] { "USD", "EUR", "CNY", "TWD", "JPY", "GBP", "HKD" };
        foreach (var curr in currencies)
        {
            if (fullText.Contains(curr))
                return curr;
        }
        return "USD"; // Default
    }

    private decimal? ExtractTotalAmount(string fullText)
    {
        var match = System.Text.RegularExpressions.Regex.Match(fullText, 
            @"(?:TOTAL|AMOUNT|合計)[\s:]*[\$¥€]*\s*([\d,]+\.?\d*)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(",", ""), out var amount))
        {
            return amount;
        }
        return null;
    }

    private List<Dictionary<string, object?>> ExtractItems(AnalyzeResult analyzeResult, string fullText)
    {
        var items = new List<Dictionary<string, object?>>();
        
        // 如果有表格，優先使用表格結構
        if (analyzeResult.Tables != null && analyzeResult.Tables.Count > 0)
        {
            foreach (var table in analyzeResult.Tables)
            {
                var itemCount = 0;
                for (int i = 1; i < table.RowCount; i++) // Skip header
                {
                    var item = new Dictionary<string, object?>();
                    
                    // Extract cells from this row
                    var row = table.Cells.Where(c => c.RowIndex == i).OrderBy(c => c.ColumnIndex).ToList();
                    
                    if (row.Count > 0) item["lineNo"] = i;
                    if (row.Count > 0) item["itemNo"] = row[0].Content?.Trim();
                    if (row.Count > 1) item["description"] = row[1].Content?.Trim();
                    if (row.Count > 2 && decimal.TryParse(row[2].Content, out var qty)) item["quantity"] = qty;
                    if (row.Count > 3) item["unit"] = row[3].Content?.Trim();
                    if (row.Count > 4 && decimal.TryParse(row[4].Content, out var price)) item["unitPrice"] = price;
                    if (row.Count > 5 && decimal.TryParse(row[5].Content, out var amount)) item["amount"] = amount;
                    if (row.Count > 6) item["poNo"] = row[6].Content?.Trim();

                    if (item.Count > 0) items.Add(item);
                    itemCount++;
                }
            }
            
            // 如果表格成功提取了項目，返回
            if (items.Count > 0) return items;
        }

        // 如果沒有表格或表格為空，使用基於行位置的方法
        return ExtractItemsFromLines(analyzeResult);
    }

    /// <summary>
    /// 基於行位置資訊從 OCR 行提取品項（當表格提取失敗時）
    /// </summary>
    private List<Dictionary<string, object?>> ExtractItemsFromLines(AnalyzeResult analyzeResult)
    {
        var items = new List<Dictionary<string, object?>>();
        
        if (analyzeResult.Pages == null || analyzeResult.Pages.Count == 0)
            return items;

        var page = analyzeResult.Pages[0];
        if (page.Lines == null || page.Lines.Count == 0)
            return items;

        var lines = page.Lines.ToList();
        
        // 查找表格開始位置（通常在 "Item No" 或 "Quantity" 之後）
        var tableStartIdx = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            var text = lines[i].Content?.ToUpperInvariant() ?? "";
            if (text.Contains("ITEM NO") || (text.Contains("QUANTITY") && text.Contains("UNIT PRICE")))
            {
                tableStartIdx = i + 1;
                break;
            }
        }

        if (tableStartIdx == -1) return items;

        // 查找表格結束位置（通常在 "TOTAL" 之前）
        var tableEndIdx = lines.Count;
        for (int i = tableStartIdx; i < lines.Count; i++)
        {
            if (lines[i].Content?.ToUpperInvariant().Contains("TOTAL") == true)
            {
                tableEndIdx = i;
                break;
            }
        }

        // 提取品項行（通常起始於行號 1, 2, 3... 的數字行）
        var itemLines = new List<List<DocumentLine>>();
        var currentItem = new List<DocumentLine>();

        for (int i = tableStartIdx; i < tableEndIdx; i++)
        {
            var line = lines[i];
            var content = line.Content?.Trim() ?? "";
            
            // 檢查是否是行號（1-99 的數字）
            if (System.Text.RegularExpressions.Regex.IsMatch(content, @"^(\d{1,2})$") && currentItem.Count > 0)
            {
                // 新行號意味著新項目開始
                itemLines.Add(currentItem);
                currentItem = new List<DocumentLine> { line };
            }
            else if (!string.IsNullOrWhiteSpace(content))
            {
                currentItem.Add(line);
            }
        }

        // 添加最後一項
        if (currentItem.Count > 0)
        {
            itemLines.Add(currentItem);
        }

        // 從品項行群組解析品項
        var lineNum = 1;
        foreach (var itemLineGroup in itemLines)
        {
            var item = new Dictionary<string, object?>();
            
            if (itemLineGroup.Count == 0) continue;

            // 第一行應該是行號
            var firstLine = itemLineGroup[0].Content?.Trim() ?? "";
            if (!int.TryParse(firstLine, out var lineNo))
                lineNo = lineNum;

            item["lineNo"] = lineNo;

            // 嘗試從後續行提取字段
            var combinedText = string.Join(" | ", itemLineGroup.Select(l => l.Content));
            
            // 提取品項編號（通常在第 2-3 行，全大寫字母/數字組合）
            for (int i = 1; i < itemLineGroup.Count; i++)
            {
                var text = itemLineGroup[i].Content?.Trim() ?? "";
                if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^[A-Z0-9]{4,}$"))
                {
                    item["itemNo"] = text;
                    break;
                }
            }

            // 提取描述（通常是帶 "-" 的行或較長的文字）
            for (int i = 1; i < itemLineGroup.Count; i++)
            {
                var text = itemLineGroup[i].Content?.Trim() ?? "";
                if (text.Contains("-") || (text.Length > 10 && !System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d")))
                {
                    item["description"] = text;
                    break;
                }
            }

            // 提取數量（包含 "PCS" 的行）
            var qtyMatch = System.Text.RegularExpressions.Regex.Match(combinedText, @"([\d,]+)\s*PCS", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (qtyMatch.Success && decimal.TryParse(qtyMatch.Groups[1].Value.Replace(",", ""), out var qty))
            {
                item["quantity"] = qty;
                item["unit"] = "PCS";
            }

            // 提取單價（通常是 3-4 位數的小數）
            var pricePattern = @"(\d+\.\d{4})";
            var priceMatches = System.Text.RegularExpressions.Regex.Matches(combinedText, pricePattern);
            if (priceMatches.Count > 0 && decimal.TryParse(priceMatches[0].Value, out var unitPrice))
            {
                item["unitPrice"] = unitPrice;
            }

            // 提取金額（通常是最大的數字，可能帶逗號）
            var amountPattern = @"([\d,]+\.\d{2})(?!.*[\d,]+\.\d{2})"; // 最後一個金額數字
            var amountMatch = System.Text.RegularExpressions.Regex.Match(combinedText, amountPattern);
            if (amountMatch.Success && decimal.TryParse(amountMatch.Groups[1].Value.Replace(",", ""), out var amount))
            {
                item["amount"] = amount;
            }

            // 提取 PO 編號（6-9 位數字）
            var poMatch = System.Text.RegularExpressions.Regex.Match(combinedText, @"(\d{6,9})(?!\d)");
            if (poMatch.Success && !item.ContainsKey("quantity")) // 避免與數量混淆
            {
                item["poNo"] = poMatch.Groups[1].Value;
            }

            if (item.Count > 1) // 至少有行號 + 其他欄位
            {
                items.Add(item);
            }

            lineNum++;
        }

        _logger.LogInformation("Extracted {count} items from OCR lines", items.Count);
        return items;
    }

    private string? ExtractRemarks(string fullText)
    {
        var match = System.Text.RegularExpressions.Regex.Match(fullText, 
            @"(?:REMARK|NOTE|NOTES|備註)[\s:]*([^\n]+)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    #endregion

    /// <summary>
    /// Serialize AnalyzeResult for JSON output
    /// </summary>
    private object SerializeAnalyzeResult(AnalyzeResult analyzeResult)
    {
        return new
        {
            status = "succeeded",
            pages = analyzeResult.Pages?.Select(p => new
            {
                pageNumber = p.PageNumber,
                lines = p.Lines?.Select(l => new { text = l.Content })
            }),
            tables = analyzeResult.Tables?.Select(t => new
            {
                rowCount = t.RowCount,
                columnCount = t.ColumnCount,
                cells = t.Cells?.Select(c => new 
                { 
                    rowIndex = c.RowIndex,
                    columnIndex = c.ColumnIndex,
                    content = c.Content
                })
            })
        };
    }
}
