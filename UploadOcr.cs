using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Linq;
using System.Threading;

namespace wk.pdf.ocr;

/// <summary>
/// ⚠️ DEPRECATED: This is the old REST-based implementation
/// Please use UploadOcr_SDK.cs instead which uses the official Azure.AI.DocumentIntelligence SDK
/// 
/// Legacy implementation kept for reference and backward compatibility
/// Function name: upload-ocr (old)
/// New Function name: upload-ocr-sdk (recommended)
/// </summary>
public class UploadOcr
{
    private readonly ILogger<UploadOcr> _logger;
    private static readonly HttpClient _http = new HttpClient();
    public UploadOcr(ILogger<UploadOcr> logger)
    {
        _logger = logger;
    }

    [Function("upload-ocr")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "upload-ocr")] HttpRequest req)
    {
        _logger.LogInformation("HTTP trigger for PDF OCR (upload-ocr) invoked.");

        // Check if a file was uploaded via multipart/form-data
        if (req.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            req.HasFormContentType)
        {
            var form = await req.ReadFormAsync();
            var file = form.Files.Count > 0 ? form.Files[0] : null;

            if (file == null)
            {
                return new BadRequestObjectResult(new { error = "No file uploaded. Include a PDF file in form-data (field name: file)." });
            }

            // Accept PDF and TIFF formats (by filename). Also consider validating ContentType when available.
            var lower = file.FileName?.ToLowerInvariant() ?? string.Empty;
            if (!(lower.EndsWith(".pdf") || lower.EndsWith(".tif") || lower.EndsWith(".tiff")))
            {
                return new BadRequestObjectResult(new { error = "Uploaded file must be a PDF or TIFF." });
            }

            // Read file into memory (for small files). For large files, consider streaming to blob storage.
            // Enforce max file size (configurable via env AZ_OCR_MAX_FILE_BYTES, default 20MB)
            var maxBytesEnv = Environment.GetEnvironmentVariable("AZ_OCR_MAX_FILE_BYTES");
            var maxBytes = 20_000_000;
            if (!string.IsNullOrEmpty(maxBytesEnv) && int.TryParse(maxBytesEnv, out var mb)) maxBytes = mb;

            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            if (ms.Length > maxBytes)
            {
                _logger.LogWarning("Uploaded file {fileName} too large: {size} bytes (max {max})", file.FileName, ms.Length, maxBytes);
                return new ObjectResult(new { error = "Uploaded file too large" }) { StatusCode = 413 };
            }
            ms.Position = 0;

            // Call Azure Computer Vision Read (REST) API
            var endpoint = Environment.GetEnvironmentVariable("AZ_COMPUTER_VISION_ENDPOINT");
            var key = Environment.GetEnvironmentVariable("AZ_COMPUTER_VISION_KEY");

            // Polling config
            var pollMaxEnv = Environment.GetEnvironmentVariable("AZ_OCR_POLL_MAX_ATTEMPTS");
            var pollIntervalEnv = Environment.GetEnvironmentVariable("AZ_OCR_POLL_INITIAL_DELAY_MS");
            var pollMax = 40;
            var pollInterval = 500;
            if (!string.IsNullOrEmpty(pollMaxEnv) && int.TryParse(pollMaxEnv, out var pmax)) pollMax = pmax;
            if (!string.IsNullOrEmpty(pollIntervalEnv) && int.TryParse(pollIntervalEnv, out var pint)) pollInterval = pint;

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
            {
                return new ObjectResult(new { error = "Computer Vision endpoint/key not configured. Set AZ_COMPUTER_VISION_ENDPOINT and AZ_COMPUTER_VISION_KEY in settings." }) { StatusCode = 500 };
            }

            try
            {
                // ===== Document Intelligence (New Primary Service) =====
                var diEndpoint = Environment.GetEnvironmentVariable("AZ_DOCUMENT_INTELLIGENCE_ENDPOINT");
                var diKey = Environment.GetEnvironmentVariable("AZ_DOCUMENT_INTELLIGENCE_KEY");

                if (string.IsNullOrEmpty(diEndpoint) || string.IsNullOrEmpty(diKey))
                {
                    return new ObjectResult(new { error = "Document Intelligence endpoint/key not configured. Set AZ_DOCUMENT_INTELLIGENCE_ENDPOINT and AZ_DOCUMENT_INTELLIGENCE_KEY in settings." }) { StatusCode = 500 };
                }

                // Prepare Document Intelligence request
                _http.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");
                _http.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", diKey);

                var diUri = new Uri(new Uri(diEndpoint), "/documentintelligence/documentmodels/prebuilt-read:analyze?api-version=2024-02-29-preview");

                using var diContent = new StreamContent(ms);
                // set content type based on extension
                if (lower.EndsWith(".pdf")) diContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                else diContent.Headers.ContentType = new MediaTypeHeaderValue("image/tiff");

                _logger.LogInformation("Calling Document Intelligence API for file {fileName}", file.FileName);
                var diResponse = await _http.PostAsync(diUri, diContent);
                
                if (diResponse.StatusCode != HttpStatusCode.Accepted)
                {
                    var body = await diResponse.Content.ReadAsStringAsync();
                    var headers = string.Join(";", diResponse.Headers.Select(h => $"{h.Key}={string.Join(',', h.Value)}"));
                    _logger.LogError("Document Intelligence POST failed: {status}; headers: {headers}; body: {body}", diResponse.StatusCode, headers, body);
                    return new ObjectResult(new { error = "Document Intelligence POST failed", details = body }) { StatusCode = (int)diResponse.StatusCode };
                }

                // The API returns 202 with Operation-Location header
                if (!diResponse.Headers.Contains("Operation-Location"))
                {
                    var body = await diResponse.Content.ReadAsStringAsync();
                    _logger.LogError("No Operation-Location header returned from Document Intelligence. Body: {body}", body);
                    return new ObjectResult(new { error = "No Operation-Location returned", details = body }) { StatusCode = 500 };
                }

                var operationLocation = diResponse.Headers.GetValues("Operation-Location").FirstOrDefault();
                if (string.IsNullOrEmpty(operationLocation))
                {
                    _logger.LogError("Operation-Location header empty");
                    return new ObjectResult(new { error = "Operation-Location header empty" }) { StatusCode = 500 };
                }

                // Poll for result with backoff and Retry-After handling
                string? resultJson = null;
                var delay = pollInterval;
                for (int i = 0; i < pollMax; i++)
                {
                    if (i > 0) _logger.LogDebug("Waiting {delay}ms before polling attempt {attempt}", delay, i);
                    await Task.Delay(delay);

                    var getResp = await _http.GetAsync(operationLocation);
                    var text = await getResp.Content.ReadAsStringAsync();
                    _logger.LogDebug("Document Intelligence polling attempt {i}: status={status}, bodyPreview={preview}", i + 1, getResp.StatusCode, text?.Length > 300 ? text.Substring(0, 300) + "..." : text);

                    if (getResp.StatusCode == (HttpStatusCode)429)
                    {
                        // honor Retry-After if present
                        if (getResp.Headers.TryGetValues("Retry-After", out var vals))
                        {
                            var ra = vals.FirstOrDefault();
                            if (int.TryParse(ra, out var secs))
                            {
                                _logger.LogDebug("Received 429, retry-after seconds={secs}", secs);
                                await Task.Delay(TimeSpan.FromSeconds(secs));
                                delay = Math.Min(delay * 2, 10000);
                                continue;
                            }
                        }
                        delay = Math.Min(delay * 2, 10000);
                        continue;
                    }

                    try
                    {
                        using var doc = JsonDocument.Parse(string.IsNullOrEmpty(text) ? "{}" : text);
                        if (doc.RootElement.TryGetProperty("status", out var statusEl))
                        {
                            var status = statusEl.GetString();
                            if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
                            {
                                resultJson = text;
                                _logger.LogInformation("Document Intelligence analysis succeeded");
                                break;
                            }
                            else if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogError("Document Intelligence analysis failed. Details: {text}", text);
                                return new ObjectResult(new { error = "Document Intelligence analysis failed", details = text }) { StatusCode = 500 };
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // ignore and retry
                    }

                    // exponential backoff
                    delay = Math.Min(delay * 2, 10000);
                }

                if (resultJson == null)
                {
                    _logger.LogError("Timed out waiting for Document Intelligence result after {attempts} attempts", pollMax);
                    return new ObjectResult(new { error = "Timed out waiting for Document Intelligence result" }) { StatusCode = 504 };
                }

                // Parse and structure the Document Intelligence JSON result
                var parsed = JsonDocument.Parse(string.IsNullOrEmpty(resultJson) ? "{}" : resultJson).RootElement.Clone();
                
                // Call structuring function to convert to Key-Value format
                var structuredResult = StructureOCRResult(parsed, _logger);
                
                return new OkObjectResult(structuredResult);

                /* ===== Commented Out: Computer Vision (Legacy) =====
                // Prepare request
                _http.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");
                _http.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);

                var uri = new Uri(new Uri(endpoint), "/vision/v3.2/read/analyze");

                using var content = new StreamContent(ms);
                // set content type based on extension
                if (lower.EndsWith(".pdf")) content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                else content.Headers.ContentType = new MediaTypeHeaderValue("image/tiff");

                var response = await _http.PostAsync(uri, content);
                if (response.StatusCode != HttpStatusCode.Accepted)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    var headers = string.Join(";", response.Headers.Select(h => $"{h.Key}={string.Join(',', h.Value)}"));
                    _logger.LogError("Computer Vision POST failed: {status}; headers: {headers}; body: {body}", response.StatusCode, headers, body);
                    return new ObjectResult(new { error = "Computer Vision POST failed", details = body }) { StatusCode = (int)response.StatusCode };
                }

                // The API returns 202 with Operation-Location header
                if (!response.Headers.Contains("Operation-Location"))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogError("No Operation-Location header returned. Body: {body}", body);
                    return new ObjectResult(new { error = "No Operation-Location returned", details = body }) { StatusCode = 500 };
                }

                var legacyOperationLocation = response.Headers.GetValues("Operation-Location").FirstOrDefault();
                if (string.IsNullOrEmpty(legacyOperationLocation))
                {
                    _logger.LogError("Operation-Location header empty");
                    return new ObjectResult(new { error = "Operation-Location header empty" }) { StatusCode = 500 };
                }

                // Poll for result with backoff and Retry-After handling
                string? legacyResultJson = null;
                var legacyDelay = pollInterval;
                for (int i = 0; i < pollMax; i++)
                {
                    if (i > 0) _logger.LogDebug("Waiting {delay}ms before polling attempt {attempt}", legacyDelay, i);
                    await Task.Delay(legacyDelay);

                    var getResp = await _http.GetAsync(legacyOperationLocation);
                    var text = await getResp.Content.ReadAsStringAsync();
                    _logger.LogDebug("Polling attempt {i}: status={status}, bodyPreview={preview}", i + 1, getResp.StatusCode, text?.Length > 300 ? text.Substring(0, 300) + "..." : text);

                    if (getResp.StatusCode == (HttpStatusCode)429)
                    {
                        // honor Retry-After if present
                        if (getResp.Headers.TryGetValues("Retry-After", out var vals))
                        {
                            var ra = vals.FirstOrDefault();
                            if (int.TryParse(ra, out var secs))
                            {
                                _logger.LogDebug("Received 429, retry-after seconds={secs}", secs);
                                await Task.Delay(TimeSpan.FromSeconds(secs));
                                legacyDelay = Math.Min(legacyDelay * 2, 10000);
                                continue;
                            }
                        }
                        legacyDelay = Math.Min(legacyDelay * 2, 10000);
                        continue;
                    }

                    try
                    {
                        using var doc = JsonDocument.Parse(string.IsNullOrEmpty(text) ? "{}" : text);
                        if (doc.RootElement.TryGetProperty("status", out var statusEl))
                        {
                            var status = statusEl.GetString();
                            if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
                            {
                                legacyResultJson = text;
                                break;
                            }
                            else if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogError("OCR failed during polling. Details: {text}", text);
                                return new ObjectResult(new { error = "OCR failed", details = text }) { StatusCode = 500 };
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // ignore and retry
                    }

                    // exponential backoff
                    legacyDelay = Math.Min(legacyDelay * 2, 10000);
                }

                if (legacyResultJson == null)
                {
                    _logger.LogError("Timed out waiting for OCR result after {attempts} attempts", pollMax);
                    return new ObjectResult(new { error = "Timed out waiting for OCR result" }) { StatusCode = 504 };
                }

                // Return OCR JSON to caller
                var legacyParsed = JsonDocument.Parse(string.IsNullOrEmpty(legacyResultJson) ? "{}" : legacyResultJson).RootElement.Clone();
                return new OkObjectResult(legacyParsed);
                ===== End of Computer Vision Legacy Code ===== */
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while calling OCR service for file {fileName}", file.FileName);
                // return full exception details for local/dev debugging
                return new ObjectResult(new { error = "Exception calling OCR", details = ex.ToString() }) { StatusCode = 500 };
            }
        }

        // Default / help response for GET or non-form requests
        return new OkObjectResult(new
        {
            message = "Upload a PDF via POST multipart/form-data (field name: file).",
            env = new[] { "AZ_DOCUMENT_INTELLIGENCE_ENDPOINT", "AZ_DOCUMENT_INTELLIGENCE_KEY", "AZ_COMPUTER_VISION_ENDPOINT (deprecated)", "AZ_COMPUTER_VISION_KEY (deprecated)" }
        });
    }

    /// <summary>
    /// 將 Document Intelligence 的 OCR 結果結構化為簡潔的 Key-Value JSON 格式
    /// </summary>
    private static object StructureOCRResult(JsonElement analyzeResult, ILogger<UploadOcr> logger)
    {
        try
        {
            logger.LogInformation("Starting to structure OCR result...");

            // 提取原始 OCR 文本
            string fullText = string.Empty;
            if (analyzeResult.TryGetProperty("analyzeResult", out var analyzeObj))
            {
                if (analyzeObj.TryGetProperty("content", out var contentEl))
                {
                    fullText = contentEl.GetString() ?? string.Empty;
                }
            }

            logger.LogDebug("Extracted full text length: {length}", fullText.Length);

            // 創建結構化結果
            var structuredData = new
            {
                meta = new
                {
                    apiVersion = "2024-02-29-preview",
                    model = "prebuilt-read",
                    pageCount = GetPageCount(analyzeResult),
                    timestamp = DateTime.UtcNow.ToString("O")
                },
                extracted = ExtractInvoiceFields(fullText, analyzeResult, logger),
                fullText = fullText,
                raw = analyzeResult // 保留原始數據供參考
            };

            logger.LogInformation("OCR structuring completed successfully");
            return structuredData;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during OCR result structuring");
            return new
            {
                error = "Structuring failed",
                details = ex.Message,
                raw = analyzeResult
            };
        }
    }

    /// <summary>
    /// 從 OCR 文本中提取發票欄位
    /// 支持多種發票格式的自動識別
    /// </summary>
    private static dynamic ExtractInvoiceFields(string fullText, JsonElement analyzeResult, ILogger<UploadOcr> logger)
    {
        var extracted = new Dictionary<string, object?>
        {
            // 基本資訊
            { "invoiceNo", ExtractInvoiceNumber(fullText) },
            { "date", ExtractDate(fullText) },
            
            // 賣方資訊
            { "seller", ExtractSeller(fullText) },
            { "sellerAddress", ExtractSellerAddress(fullText) },
            { "contact", ExtractContact(fullText) },
            
            // 買方資訊
            { "buyer", ExtractBuyer(fullText) },
            { "buyerAddress", ExtractBuyerAddress(fullText) },
            
            // 交易條款
            { "tradeTerm", ExtractTradeTerm(fullText) },
            { "origin", ExtractOrigin(fullText) },
            
            // 品項明細
            { "items", ExtractItems(fullText) },
            
            // 金額資訊
            { "totalAmount", ExtractTotalAmount(fullText) },
            { "currency", ExtractCurrency(fullText) }
        };

        logger.LogDebug("Extracted fields: {fields}", string.Join(", ", extracted.Keys));
        return extracted;
    }

    #region 欄位提取方法

    private static int GetPageCount(JsonElement analyzeResult)
    {
        if (analyzeResult.TryGetProperty("analyzeResult", out var analyzeObj) &&
            analyzeObj.TryGetProperty("pages", out var pages))
        {
            return pages.GetArrayLength();
        }
        return 1;
    }

    private static string? ExtractInvoiceNumber(string fullText)
    {
        // 模式 1: Invoice No: XXX
        var match = System.Text.RegularExpressions.Regex.Match(
            fullText, 
            @"Invoice\s*(?:No|Number|#|編號)?[\s:]*([A-Z0-9\-]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        if (match.Success) return match.Groups[1].Value.Trim();

        // 模式 2: 純數字+字母格式 (e.g., 2020066251106A)
        match = System.Text.RegularExpressions.Regex.Match(fullText, @"\b(\d{10,14}[A-Z]{1,3})\b");
        if (match.Success) return match.Groups[1].Value;

        // 模式 3: INV 開頭
        match = System.Text.RegularExpressions.Regex.Match(fullText, @"INV[\s-]*([A-Z0-9]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value.Trim();

        return null;
    }

    private static string? ExtractDate(string fullText)
    {
        // 模式 1: DATE: 2025/11/06
        var match = System.Text.RegularExpressions.Regex.Match(
            fullText,
            @"DATE[\s:]*(\d{4}[-/]\d{1,2}[-/]\d{1,2})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        if (match.Success) return match.Groups[1].Value;

        // 模式 2: 2025-11-06 格式
        match = System.Text.RegularExpressions.Regex.Match(fullText, @"(\d{4}[-/]\d{1,2}[-/]\d{1,2})");
        if (match.Success) return match.Groups[1].Value;

        return null;
    }

    private static string? ExtractSeller(string fullText)
    {
        // 嘗試從開頭提取公司名
        var lines = fullText.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines.Take(5))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 5 && (trimmed.Contains("Inc") || trimmed.Contains("Ltd") || 
                trimmed.Contains("Co.") || trimmed.Contains("Corporation") || trimmed.Contains("Company")))
            {
                return trimmed;
            }
        }

        // 尋找特定模式
        var match = System.Text.RegularExpressions.Regex.Match(
            fullText,
            @"^([A-Z][A-Z\s]+(?:Inc|Ltd|Co|Corporation|Company))",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        if (match.Success) return match.Groups[1].Value.Trim();

        return null;
    }

    private static string? ExtractSellerAddress(string fullText)
    {
        // 尋找地址模式 (含街道號)
        var match = System.Text.RegularExpressions.Regex.Match(
            fullText,
            @"(\d+[A-Za-z]?\s+[A-Za-z\s]+(?:Street|Road|Ave|Blvd|Drive|Lane|Park|Science Park|Taipei|Hsinchu|Taiwan))",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        if (match.Success) return match.Groups[1].Value.Trim();

        return null;
    }

    private static string? ExtractContact(string fullText)
    {
        // 尋找電話號碼
        var match = System.Text.RegularExpressions.Regex.Match(
            fullText,
            @"TEL[\s:]*([0-9\-\(\)\s]+?)(?:FAX|Email|Contact|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        if (match.Success) return match.Groups[1].Value.Trim();

        return null;
    }

    private static string? ExtractBuyer(string fullText)
    {
        // 尋找 BILL TO 或 SOLD TO
        var match = System.Text.RegularExpressions.Regex.Match(
            fullText,
            @"(?:BILL\s*TO|SOLD\s*TO|BUYER)[\s:]*([A-Z][A-Z\s,\.]+?)(?:TEL|地址|ADD|ADDR|統一|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline
        );
        if (match.Success) return match.Groups[1].Value.Trim();

        return null;
    }

    private static string? ExtractBuyerAddress(string fullText)
    {
        // 尋找 BILL TO 後面的地址
        var match = System.Text.RegularExpressions.Regex.Match(
            fullText,
            @"(?:BILL\s*TO|SOLD\s*TO)[\s:]*[A-Z\s,\.]+?([A-Z0-9\s,\.\-]+(?:Street|Road|Taipei|Hong Kong|City|District))",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline
        );
        if (match.Success) return match.Groups[1].Value.Trim();

        return null;
    }

    private static string? ExtractTradeTerm(string fullText)
    {
        // 尋找貿易條款 (FOB, CIF, CFR 等)
        var match = System.Text.RegularExpressions.Regex.Match(
            fullText,
            @"(?:Trade\s*Term|Incoterm)[\s:]*([A-Z\s]+?)(?:\n|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        if (match.Success) return match.Groups[1].Value.Trim();

        // 直接尋找 FOB, CIF 等
        match = System.Text.RegularExpressions.Regex.Match(fullText, @"\b(FOB|CIF|CFR|EXW|DAP|DDP)\b");
        if (match.Success) return match.Groups[1].Value;

        return null;
    }

    private static string? ExtractOrigin(string fullText)
    {
        // 尋找原產地
        var match = System.Text.RegularExpressions.Regex.Match(
            fullText,
            @"(?:Country\s*of\s*)?Origin[\s:]*([A-Z][A-Za-z\s]+?)(?:\n|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        if (match.Success) return match.Groups[1].Value.Trim();

        // 尋找 CHINA, TAIWAN 等
        match = System.Text.RegularExpressions.Regex.Match(fullText, @"\b(CHINA|TAIWAN|JAPAN|USA|KOREA)\b");
        if (match.Success) return match.Groups[1].Value;

        return null;
    }

    private static string? ExtractCurrency(string fullText)
    {
        // 尋找貨幣代碼
        var match = System.Text.RegularExpressions.Regex.Match(fullText, @"\b(USD|EUR|CNY|TWD|JPY|GBP)\b");
        if (match.Success) return match.Groups[1].Value;

        // 預設 USD
        return "USD";
    }

    private static decimal? ExtractTotalAmount(string fullText)
    {
        // 尋找 TOTAL 後面的數字
        var match = System.Text.RegularExpressions.Regex.Match(
            fullText,
            @"TOTAL[\s:]*(?:USD|CNY|TWD|JPY|EUR)?[\s]*([0-9,]+\.?\d*)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        if (match.Success)
        {
            var numberStr = match.Groups[1].Value.Replace(",", "");
            if (decimal.TryParse(numberStr, out var amount))
                return amount;
        }

        return null;
    }

    private static dynamic ExtractItems(string fullText)
    {
        // 尋找表格中的品項
        var items = new List<Dictionary<string, object?>>();
        
        // 簡單的表格行解析
        // 尋找包含數量、單價、金額的行
        var lines = fullText.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var inTable = false;
        var lineNo = 0;

        foreach (var line in lines)
        {
            var upper = line.ToUpperInvariant();
            
            // 檢測表格開始
            if (upper.Contains("QUANTITY") || upper.Contains("QTY") || upper.Contains("ITEM"))
                inTable = true;
            
            // 檢測表格結束
            if (inTable && (upper.Contains("TOTAL") || upper.Contains("SUBTOTAL")))
                break;

            if (inTable && line.Length > 10)
            {
                lineNo++;
                var item = new Dictionary<string, object?>
                {
                    { "lineNo", lineNo.ToString() },
                    { "itemNo", ExtractItemNo(line) },
                    { "description", ExtractDescription(line) },
                    { "quantity", ExtractQuantity(line) },
                    { "unit", ExtractUnit(line) },
                    { "unitPrice", ExtractUnitPrice(line) },
                    { "amount", ExtractAmount(line) },
                    { "poNo", ExtractPoNo(line) }
                };
                
                if (item["quantity"] != null)
                    items.Add(item);
            }
        }

        return items;
    }

    private static string? ExtractItemNo(string line)
    {
        var match = System.Text.RegularExpressions.Regex.Match(line, @"\b([A-Z][A-Z0-9\-]{5,20})\b");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractDescription(string line)
    {
        // 尋找描述文本 (通常是中文或英文)
        var parts = line.Split(new[] { " ", "\t" }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? string.Join(" ", parts.Skip(1).Take(3)) : null;
    }

    private static int? ExtractQuantity(string line)
    {
        var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)\s*(?:PCS|EA|UNITS|個)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var qty) ? qty : null;
    }

    private static string? ExtractUnit(string line)
    {
        var match = System.Text.RegularExpressions.Regex.Match(line, @"\b(PCS|EA|BOX|CASE|KG|M|PACK|UNITS|SET|LOT)\b");
        return match.Success ? match.Groups[1].Value : "PCS";
    }

    private static decimal? ExtractUnitPrice(string line)
    {
        var match = System.Text.RegularExpressions.Regex.Match(line, @"[^\d](\d+\.\d{2,4})\s*(?:USD|$)");
        return match.Success && decimal.TryParse(match.Groups[1].Value, out var price) ? price : null;
    }

    private static decimal? ExtractAmount(string line)
    {
        var match = System.Text.RegularExpressions.Regex.Match(line, @"\$?\s*([0-9,]+\.\d{2})\s*(?:$|USD|CNY)");
        if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(",", ""), out var amount))
            return amount;
        return null;
    }

    private static string? ExtractPoNo(string line)
    {
        var match = System.Text.RegularExpressions.Regex.Match(line, @"\b(PO[\s\-]?[0-9]+|[0-9]{9,12})\b");
        return match.Success ? match.Groups[1].Value : null;
    }

    #endregion
}
