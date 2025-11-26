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

            _logger.LogInformation("Calling Document Intelligence API with prebuilt-read model");
            var operation = await client.AnalyzeDocumentAsync(
                WaitUntil.Completed, 
                "prebuilt-read", 
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

        var extracted = ExtractInvoiceFields(analyzeResult, fullText);

        return new
        {
            meta = new
            {
                apiVersion = "2024-02-29-preview",
                model = "prebuilt-read",
                pageCount = analyzeResult.Pages?.Count ?? 0,
                timestamp = DateTime.UtcNow.ToString("O"),
                fileName = fileName
            },
            extracted = extracted,
            fullText = fullText,
            raw = SerializeAnalyzeResult(analyzeResult)
        };
    }

    /// <summary>
    /// Extract invoice fields from analyze result
    /// </summary>
    private Dictionary<string, object?> ExtractInvoiceFields(AnalyzeResult analyzeResult, string fullText)
    {
        var fields = new Dictionary<string, object?>();

        // Extract basic fields
        fields["invoiceNo"] = ExtractInvoiceNumber(fullText);
        fields["date"] = ExtractDate(fullText);
        fields["seller"] = ExtractSeller(fullText, analyzeResult);
        fields["sellerAddress"] = ExtractSellerAddress(fullText);
        fields["contact"] = ExtractContact(fullText);
        fields["buyer"] = ExtractBuyer(fullText);
        fields["buyerAddress"] = ExtractBuyerAddress(fullText);
        fields["tradeTerm"] = ExtractTradeTerm(fullText);
        fields["origin"] = ExtractOrigin(fullText);
        fields["currency"] = ExtractCurrency(fullText);
        fields["totalAmount"] = ExtractTotalAmount(fullText);
        
        // Extract items from tables
        fields["items"] = ExtractItems(analyzeResult, fullText);
        fields["remarks"] = ExtractRemarks(fullText);

        return fields;
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
