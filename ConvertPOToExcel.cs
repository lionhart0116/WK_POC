using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace WKPdfOcr
{
    public class ConvertPOToExcel
    {
        private readonly ILogger<ConvertPOToExcel> _logger;

        public ConvertPOToExcel(ILogger<ConvertPOToExcel> logger)
        {
            _logger = logger;
        }

        [Function("convert-po-to-excel")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "convert-po-to-excel")] HttpRequestData req)
        {
            _logger.LogInformation("Convert PO to Excel request received");

            try
            {
                // 讀取請求內容
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<ConvertPORequest>(requestBody);

                if (data == null || string.IsNullOrWhiteSpace(data.PoJson))
                {
                    _logger.LogWarning("Invalid request: Missing PO JSON");
                    var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Missing PO JSON data");
                    return badResponse;
                }

                // 驗證格式
                var format = data.Format?.ToUpper() ?? "CO";
                if (!IsValidFormat(format))
                {
                    _logger.LogWarning($"Invalid format: {format}");
                    var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync($"Invalid format: {format}. Supported formats: CO, SO, TWOSTAGE");
                    return badResponse;
                }

                _logger.LogInformation($"Converting PO to {format} format");

                // 轉換為 Excel
                byte[] excelBytes = POExcelConverter.ConvertToExcel(
                    data.PoJson,
                    format,
                    data.ParamValue ?? "AUTO"
                );

                _logger.LogInformation($"Excel generated successfully, size: {excelBytes.Length} bytes");

                // 設定檔案名稱
                string fileName = format switch
                {
                    "SO" => $"SO_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                    "TWOSTAGE" or "兩階段" or "2STAGE" => $"兩階段_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                    _ => $"CO_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                // 建立成功回應
                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                await response.Body.WriteAsync(excelBytes, 0, excelBytes.Length);

                return response;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON parsing error");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync($"Invalid JSON: {ex.Message}");
                return errorResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting PO to Excel");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        private bool IsValidFormat(string format)
        {
            return format switch
            {
                "CO" => true,
                "SO" => true,
                "TWOSTAGE" => true,
                "兩階段" => true,
                "2STAGE" => true,
                _ => false
            };
        }
    }

    /// <summary>
    /// 採購單轉換請求模型
    /// </summary>
    public class ConvertPORequest
    {
        /// <summary>
        /// 採購單 JSON 字串
        /// </summary>
        public string PoJson { get; set; } = string.Empty;

        /// <summary>
        /// 輸出格式: CO, SO, TWOSTAGE
        /// </summary>
        public string Format { get; set; } = "CO";

        /// <summary>
        /// 參數值 (CO: Co_Number, SO: Upload_No, TWOSTAGE: 訂單名)
        /// </summary>
        public string? ParamValue { get; set; }
    }
}
