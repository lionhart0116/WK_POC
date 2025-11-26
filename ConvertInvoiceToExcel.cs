using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;

namespace Company.Function;

public class ConvertInvoiceToExcel
{
    private readonly ILogger<ConvertInvoiceToExcel> _logger;

    public ConvertInvoiceToExcel(ILogger<ConvertInvoiceToExcel> logger)
    {
        _logger = logger;
    }

    [Function("ConvertInvoiceToExcel")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "convert-invoice-to-excel")] HttpRequestData req)
    {
        _logger.LogInformation("Convert invoice to Excel function triggered");

        try
        {
            // 讀取請求體
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var requestData = JsonConvert.DeserializeObject<ExcelConvertRequest>(requestBody);

            if (requestData == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request format");
            }

            // 驗證必要欄位
            if (string.IsNullOrEmpty(requestData.OcrJson))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "OCR JSON is required");
            }

            if (string.IsNullOrEmpty(requestData.Format) || (requestData.Format != "406" && requestData.Format != "407"))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Format must be '406' or '407'");
            }

            // 調用轉換方法
            byte[] excelBytes;
            try
            {
                if (requestData.Format == "406")
                {
                    excelBytes = InvoiceExcelConverter.ConvertToExcel406(
                        requestData.OcrJson, 
                        requestData.ParamValue ?? "AUTO_GENERATED");
                }
                else
                {
                    excelBytes = InvoiceExcelConverter.ConvertToExcel407(
                        requestData.OcrJson, 
                        requestData.ParamValue ?? "TW1411");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Conversion error: {ex.Message}");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, 
                    $"Conversion failed: {ex.Message}");
            }

            // 建立 Excel 回應
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Headers.Add("Content-Disposition", 
                $"attachment; filename=Invoice_{requestData.Format}INF_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            
            await response.Body.WriteAsync(excelBytes, 0, excelBytes.Length);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error: {ex.Message}\n{ex.StackTrace}");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, 
                "An unexpected error occurred");
        }
    }

    private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        var response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteAsJsonAsync(new { error = message });
        return response;
    }
}

/// <summary>
/// Excel 轉換請求模型
/// </summary>
public class ExcelConvertRequest
{
    [JsonProperty("ocrJson")]
    public string? OcrJson { get; set; }

    [JsonProperty("format")]
    public string? Format { get; set; } // "406" 或 "407"

    [JsonProperty("paramValue")]
    public string? ParamValue { get; set; } // 批次名稱或代理人代碼
}
