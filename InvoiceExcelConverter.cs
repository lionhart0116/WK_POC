using System;
using System.Collections.Generic;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Newtonsoft.Json.Linq;

/// <summary>
/// OCR JSON 轉換為 Excel 格式（406INF 或 407INF）
/// </summary>
public class InvoiceExcelConverter
{
    static InvoiceExcelConverter()
    {
        // 設置 EPPlus License Context (NonCommercial)
        #pragma warning disable CS0219
        OfficeOpenXml.LicenseContext lc = LicenseContext.NonCommercial;
        #pragma warning restore CS0219
    }

    // 406INF 格式的標題列 (36 columns)
    private static readonly List<string> Headers406 = new List<string>
    {
        "Batch_Name", "Vendor Num", "Vendor Site Code", "Type", "PO No",
        "PO Line Num", "Release", "Shipment Num", "Vendor Item", "Item No",
        "Qty", "Ship-To", "Subinventory", "Release Price", "INV NO",
        "Invoice Date", "Receipt Num", "Receiving Line", "Tax ID", "Item Description",
        "UOM", "Unit Price", "Amount", "Currency", "Tax Rate",
        "Tax Amount", "Total Amount", "Status", "Approval Status", "Notes",
        "Created By", "Creation Date", "Last Update By", "Last Update Date", "Remarks",
        "Customs", "Vendor Name"
    };

    // 407INF 格式的標題列
    private static readonly List<string> Headers407 = new List<string>
    {
        "Vendor_doc_num", "PO_NUM", "PO_LINE_NUM", "Item", "Agent Name",
        "Quantity", "Price", "Promised Date", "Destin_Num", "Ship_to",
        "Deliver_to", "Subinventory", "Invoice Date", "Tax ID", "Item Description",
        "UOM", "Unit Price", "Amount", "Currency", "Tax Rate",
        "Tax Amount", "Total Amount", "Status", "Approval Status", "Notes",
        "Created By", "Creation Date", "Last Update By", "Last Update Date", "Remarks"
    };

    /// <summary>
    /// 將 OCR JSON 轉換為 406INF Excel 格式
    /// 406INF: 採購訂單 + 收貨格式（36 columns）
    /// </summary>
    public static byte[] ConvertToExcel406(string ocrJsonString, string batchName = "AUTO_GENERATED")
    {
        try
        {
            var json = JObject.Parse(ocrJsonString);
            var workbook = new ExcelPackage();
            var worksheet = workbook.Workbook.Worksheets.Add("Invoice");

            // 寫入標題列
            for (int col = 0; col < Headers406.Count; col++)
            {
                var cell = worksheet.Cells[1, col + 1];
                cell.Value = Headers406[col];
                cell.Style.Font.Bold = true;
                cell.Style.Font.Size = 11;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(217, 217, 217)); // Light Gray
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                cell.Style.WrapText = true;
            }

            // 提取 OCR 資料
            var invoiceNo = json["invoiceNo"]?.ToString() ?? "";
            var invoiceDate = json["date"]?.ToString() ?? DateTime.Now.ToString("yyyy-MM-dd");
            var seller = json["seller"]?.ToString() ?? "";
            var currency = json["currency"]?.ToString() ?? "USD";
            var items = json["items"] as JArray ?? new JArray();

            // 寫入行資料
            int rowIndex = 2;
            foreach (var item in items)
            {
                try
                {
                    var poNo = item["poNo"]?.ToString() ?? "";
                    var itemNo = item["itemNo"]?.ToString() ?? "";
                    var qty = item["quantity"]?.Value<int>() ?? 0;
                    var unitPrice = item["unitPrice"]?.Value<decimal>() ?? 0m;
                    var amount = item["amount"]?.Value<decimal>() ?? (qty * unitPrice);
                    var description = item["description"]?.ToString() ?? "";

                    // 406INF 格式映射
                    worksheet.Cells[rowIndex, 1].Value = batchName;                    // Batch_Name
                    worksheet.Cells[rowIndex, 2].Value = seller;                       // Vendor Num
                    worksheet.Cells[rowIndex, 4].Value = "";                           // Type
                    worksheet.Cells[rowIndex, 5].Value = poNo;                         // PO No
                    worksheet.Cells[rowIndex, 6].Value = itemNo;                       // PO Line Num
                    worksheet.Cells[rowIndex, 10].Value = itemNo;                      // Item No
                    worksheet.Cells[rowIndex, 11].Value = qty;                         // Qty
                    worksheet.Cells[rowIndex, 12].Value = "";                          // Ship-To
                    worksheet.Cells[rowIndex, 13].Value = "";                          // Subinventory
                    worksheet.Cells[rowIndex, 14].Value = unitPrice;                   // Release Price
                    worksheet.Cells[rowIndex, 15].Value = invoiceNo;                   // INV NO
                    worksheet.Cells[rowIndex, 16].Value = invoiceDate;                 // Invoice Date
                    worksheet.Cells[rowIndex, 20].Value = description;                 // Item Description
                    worksheet.Cells[rowIndex, 21].Value = "";                          // UOM
                    worksheet.Cells[rowIndex, 22].Value = unitPrice;                   // Unit Price
                    worksheet.Cells[rowIndex, 23].Value = amount;                      // Amount
                    worksheet.Cells[rowIndex, 24].Value = currency;                    // Currency
                    worksheet.Cells[rowIndex, 25].Value = "";                          // Tax Rate
                    worksheet.Cells[rowIndex, 26].Value = "";                          // Tax Amount
                    worksheet.Cells[rowIndex, 27].Value = amount;                      // Total Amount
                    worksheet.Cells[rowIndex, 28].Value = "";                          // Status
                    worksheet.Cells[rowIndex, 29].Value = "";                          // Approval Status
                    worksheet.Cells[rowIndex, 30].Value = "";                          // Notes
                    worksheet.Cells[rowIndex, 31].Value = "";                          // Created By
                    worksheet.Cells[rowIndex, 32].Value = "";                          // Creation Date
                    worksheet.Cells[rowIndex, 33].Value = "";                          // Last Update By
                    worksheet.Cells[rowIndex, 34].Value = "";                          // Last Update Date
                    worksheet.Cells[rowIndex, 36].Value = seller;                      // Vendor Name

                    // 格式化數字欄位
                    worksheet.Cells[rowIndex, 11].Style.Numberformat.Format = "0";           // Qty
                    worksheet.Cells[rowIndex, 14].Style.Numberformat.Format = "#,##0.0000"; // Release Price
                    worksheet.Cells[rowIndex, 22].Style.Numberformat.Format = "#,##0.0000"; // Unit Price
                    worksheet.Cells[rowIndex, 23].Style.Numberformat.Format = "#,##0.00";   // Amount
                    worksheet.Cells[rowIndex, 27].Style.Numberformat.Format = "#,##0.00";   // Total Amount

                    rowIndex++;
                }
                catch (Exception ex)
                {
                    // 記錄單一行的錯誤，繼續處理下一行
                    Console.WriteLine($"Error processing item: {ex.Message}");
                }
            }

            // 調整欄寬和行高
            worksheet.Row(1).Height = 25;
            for (int col = 1; col <= Headers406.Count; col++)
            {
                worksheet.Column(col).Width = 14;
            }

            return workbook.GetAsByteArray();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to convert to Excel 406INF format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 將 OCR JSON 轉換為 407INF Excel 格式
    /// 407INF: 供應商發票格式（30 columns）
    /// </summary>
    public static byte[] ConvertToExcel407(string ocrJsonString, string agentName = "TW1411")
    {
        try
        {
            var json = JObject.Parse(ocrJsonString);
            var workbook = new ExcelPackage();
            var worksheet = workbook.Workbook.Worksheets.Add("Invoice");

            // 寫入標題列
            for (int col = 0; col < Headers407.Count; col++)
            {
                var cell = worksheet.Cells[1, col + 1];
                cell.Value = Headers407[col];
                cell.Style.Font.Bold = true;
                cell.Style.Font.Size = 11;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(217, 217, 217)); // Light Gray
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                cell.Style.WrapText = true;
            }

            // 提取 OCR 資料
            var invoiceNo = json["invoiceNo"]?.ToString() ?? "";
            var invoiceDate = json["date"]?.ToString() ?? DateTime.Now.ToString("yyyy-MM-dd");
            var currency = json["currency"]?.ToString() ?? "USD";
            var items = json["items"] as JArray ?? new JArray();

            // 寫入行資料
            int rowIndex = 2;
            foreach (var item in items)
            {
                try
                {
                    var poNo = item["poNo"]?.ToString() ?? "";
                    var itemNo = item["itemNo"]?.ToString() ?? "";
                    var qty = item["quantity"]?.Value<int>() ?? 0;
                    var unitPrice = item["unitPrice"]?.Value<decimal>() ?? 0m;
                    var amount = item["amount"]?.Value<decimal>() ?? (qty * unitPrice);
                    var description = item["description"]?.ToString() ?? "";

                    // 407INF 格式映射
                    worksheet.Cells[rowIndex, 1].Value = invoiceNo;                    // Vendor_doc_num
                    worksheet.Cells[rowIndex, 2].Value = poNo;                         // PO_NUM
                    worksheet.Cells[rowIndex, 3].Value = itemNo;                       // PO_LINE_NUM
                    worksheet.Cells[rowIndex, 4].Value = description;                  // Item
                    worksheet.Cells[rowIndex, 5].Value = agentName;                    // Agent Name
                    worksheet.Cells[rowIndex, 6].Value = qty;                          // Quantity
                    worksheet.Cells[rowIndex, 7].Value = unitPrice;                    // Price
                    worksheet.Cells[rowIndex, 8].Value = invoiceDate;                  // Promised Date
                    worksheet.Cells[rowIndex, 9].Value = "";                           // Destin_Num
                    worksheet.Cells[rowIndex, 10].Value = "";                          // Ship_to
                    worksheet.Cells[rowIndex, 11].Value = "";                          // Deliver_to
                    worksheet.Cells[rowIndex, 12].Value = "";                          // Subinventory
                    worksheet.Cells[rowIndex, 13].Value = invoiceDate;                 // Invoice Date
                    worksheet.Cells[rowIndex, 15].Value = description;                 // Item Description
                    worksheet.Cells[rowIndex, 16].Value = "";                          // UOM
                    worksheet.Cells[rowIndex, 17].Value = unitPrice;                   // Unit Price
                    worksheet.Cells[rowIndex, 18].Value = amount;                      // Amount
                    worksheet.Cells[rowIndex, 19].Value = currency;                    // Currency
                    worksheet.Cells[rowIndex, 20].Value = "";                          // Tax Rate
                    worksheet.Cells[rowIndex, 21].Value = "";                          // Tax Amount
                    worksheet.Cells[rowIndex, 22].Value = amount;                      // Total Amount
                    worksheet.Cells[rowIndex, 23].Value = "";                          // Status
                    worksheet.Cells[rowIndex, 24].Value = "";                          // Approval Status
                    worksheet.Cells[rowIndex, 25].Value = "";                          // Notes
                    worksheet.Cells[rowIndex, 26].Value = "";                          // Created By
                    worksheet.Cells[rowIndex, 27].Value = "";                          // Creation Date
                    worksheet.Cells[rowIndex, 28].Value = "";                          // Last Update By
                    worksheet.Cells[rowIndex, 29].Value = "";                          // Last Update Date

                    // 格式化數字欄位
                    worksheet.Cells[rowIndex, 6].Style.Numberformat.Format = "0";            // Quantity
                    worksheet.Cells[rowIndex, 7].Style.Numberformat.Format = "#,##0.0000";  // Price
                    worksheet.Cells[rowIndex, 17].Style.Numberformat.Format = "#,##0.0000"; // Unit Price
                    worksheet.Cells[rowIndex, 18].Style.Numberformat.Format = "#,##0.00";   // Amount
                    worksheet.Cells[rowIndex, 22].Style.Numberformat.Format = "#,##0.00";   // Total Amount

                    rowIndex++;
                }
                catch (Exception ex)
                {
                    // 記錄單一行的錯誤，繼續處理下一行
                    Console.WriteLine($"Error processing item: {ex.Message}");
                }
            }

            // 調整欄寬和行高
            worksheet.Row(1).Height = 25;
            for (int col = 1; col <= Headers407.Count; col++)
            {
                worksheet.Column(col).Width = 14;
            }

            return workbook.GetAsByteArray();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to convert to Excel 407INF format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 將 OCR JSON 轉換為 Excel（自動根據格式選擇）
    /// </summary>
    public static byte[] ConvertToExcel(string ocrJsonString, string format = "406", string batchNameOrAgent = "AUTO")
    {
        return format.ToUpper() == "407" 
            ? ConvertToExcel407(ocrJsonString, batchNameOrAgent)
            : ConvertToExcel406(ocrJsonString, batchNameOrAgent);
    }
}
