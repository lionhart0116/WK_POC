using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Newtonsoft.Json.Linq;

/// <summary>
/// 採購單 OCR JSON 轉換為 Excel 格式 (CO / SO / 兩階段)
/// </summary>
public class POExcelConverter
{
    static POExcelConverter()
    {
        // 設置 EPPlus License Context (NonCommercial)
        #pragma warning disable CS0219
        OfficeOpenXml.LicenseContext lc = LicenseContext.NonCommercial;
        #pragma warning restore CS0219
    }

    // CO 格式的標題列
    private static readonly List<string> HeadersCO = new List<string>
    {
        "Co_Number", "Cust_Po_Number", "Cust_Po_Line_num", "客戶料號", "廠商料號",
        "Co_Qty", "客戶品名", "Unit_Selling_Price", "Cust_Request_Date", "DPA_NO",
        "Cust_Order_Date", "N_Ver", "N_原始數量", "End_Customer", "N_CSD_Date"
    };

    // SO 格式的標題列
    private static readonly List<string> HeadersSO = new List<string>
    {
        "UPLOAD NO", "LINE", "CUSTOMER NO.", "PO NO.", "ORDER TYPE", "DATE",
        "SHIP TO", "SHIP TO ATTN", "BILL TO", "CURRENCYY", "PRICE BOOK",
        "SALES", "PAYMENT TERM", "WH", "LINE SET", "SHIPPING METHOD",
        "FOB", "SHIPPING INSTRUCTIONS", "CONVERSION TYPE", "", "",
        "P/N", "ITEM", "QTY", "PRICE", "PO", "PO LINE", "TAX CODE",
        "RD", "RD", "出貨地", "DPA", "SUB", "SHIP SET"
    };

    // 兩階段格式的標題列
    private static readonly List<string> HeadersTwoStage = new List<string>
    {
        "訂單名", "From Org", "To Org", "類別", "Transaction Type",
        "發出客戶", "發出客戶代號", "EndCust發出客戶", "EndCust發出客戶代號",
        "稅or免稅", "運送模式", "Mawb", "Hawb", "客戶料號", "數量批號",
        "WF流程號", "Ship Via", "客戶作業員", "Line Num", "From Item",
        "From Subinvnetory", "To Subinventory", "Quantity", "Po Number",
        "Invoice Number", "Vendor RMA Num", "Credit Number", "批號批號",
        "備註欄位", "Check Sum", "國家", "Do No 編號", "RMA No(Attribute1)"
    };

    /// <summary>
    /// 將採購單 JSON 轉換為 CO Excel 格式
    /// </summary>
    public static byte[] ConvertToCO(string poJsonString, string coNumber = "AUTO")
    {
        try
        {
            var json = JObject.Parse(poJsonString);
            var workbook = new ExcelPackage();
            var worksheet = workbook.Workbook.Worksheets.Add("CO");

            // 寫入標題列
            WriteHeaders(worksheet, HeadersCO);

            // 提取採購單資料
            var poNo = json["poNo"]?.ToString() ?? json["purchaseOrder"]?.ToString() ?? "";
            var poDate = json["poDate"]?.ToString() ?? json["date"]?.ToString() ?? DateTime.Now.ToString("yyyy/MM/dd");
            var buyer = json["buyer"]?.ToString() ?? "";
            var seller = json["seller"]?.ToString() ?? "";
            var items = json["items"] as JArray ?? new JArray();

            // 寫入行資料
            int rowIndex = 2;
            int lineNum = 1;
            foreach (var item in items)
            {
                try
                {
                    var itemNo = item["itemNo"]?.ToString() ?? "";
                    var description = item["description"]?.ToString() ?? "";
                    var qty = item["quantity"]?.Value<int>() ?? 0;
                    var unitPrice = item["unitPrice"]?.Value<decimal>() ?? 0m;
                    var deliveryDate = item["deliveryDate"]?.ToString() ?? "";

                    // CO 格式映射
                    worksheet.Cells[rowIndex, 1].Value = coNumber;                      // Co_Number
                    worksheet.Cells[rowIndex, 2].Value = poNo;                          // Cust_Po_Number
                    worksheet.Cells[rowIndex, 3].Value = lineNum;                       // Cust_Po_Line_num
                    worksheet.Cells[rowIndex, 4].Value = itemNo;                        // 客戶料號
                    worksheet.Cells[rowIndex, 5].Value = itemNo;                        // 廠商料號
                    worksheet.Cells[rowIndex, 6].Value = qty;                           // Co_Qty
                    worksheet.Cells[rowIndex, 7].Value = description;                   // 客戶品名
                    worksheet.Cells[rowIndex, 8].Value = unitPrice;                     // Unit_Selling_Price
                    worksheet.Cells[rowIndex, 9].Value = deliveryDate;                  // Cust_Request_Date
                    worksheet.Cells[rowIndex, 10].Value = json["dpaNo"]?.ToString() ?? "";  // DPA_NO
                    worksheet.Cells[rowIndex, 11].Value = poDate;                       // Cust_Order_Date
                    worksheet.Cells[rowIndex, 12].Value = json["nVer"]?.ToString() ?? "";   // N_Ver
                    worksheet.Cells[rowIndex, 13].Value = qty;                          // N_原始數量
                    worksheet.Cells[rowIndex, 14].Value = buyer;                        // End_Customer
                    worksheet.Cells[rowIndex, 15].Value = json["nCsdDate"]?.ToString() ?? ""; // N_CSD_Date

                    // 格式化數字欄位
                    worksheet.Cells[rowIndex, 6].Style.Numberformat.Format = "0";
                    worksheet.Cells[rowIndex, 8].Style.Numberformat.Format = "#,##0.0000";
                    worksheet.Cells[rowIndex, 13].Style.Numberformat.Format = "0";

                    rowIndex++;
                    lineNum++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing CO item: {ex.Message}");
                }
            }

            AdjustWorksheet(worksheet, HeadersCO.Count);
            return workbook.GetAsByteArray();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to convert to CO format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 將採購單 JSON 轉換為 SO Excel 格式
    /// </summary>
    public static byte[] ConvertToSO(string poJsonString, string uploadNo = "AUTO", string customerNo = "")
    {
        try
        {
            var json = JObject.Parse(poJsonString);
            var workbook = new ExcelPackage();
            var worksheet = workbook.Workbook.Worksheets.Add("SO");

            // 寫入標題列
            WriteHeaders(worksheet, HeadersSO);

            // 提取採購單資料
            var poNo = json["poNo"]?.ToString() ?? json["purchaseOrder"]?.ToString() ?? "";
            var poDate = json["poDate"]?.ToString() ?? json["date"]?.ToString() ?? DateTime.Now.ToString("yyyy/MM/dd");
            var buyer = json["buyer"]?.ToString() ?? "";
            var seller = json["seller"]?.ToString() ?? "";
            var currency = json["currency"]?.ToString() ?? "USD";
            var deliveryAddress = json["deliveryAddress"]?.ToString() ?? "";
            var contact = json["contact"]?.ToString() ?? "";
            var paymentTerm = json["paymentTerm"]?.ToString() ?? "";
            var items = json["items"] as JArray ?? new JArray();

            // 寫入行資料
            int rowIndex = 2;
            int lineNum = 1;
            foreach (var item in items)
            {
                try
                {
                    var itemNo = item["itemNo"]?.ToString() ?? "";
                    var description = item["description"]?.ToString() ?? "";
                    var qty = item["quantity"]?.Value<int>() ?? 0;
                    var unitPrice = item["unitPrice"]?.Value<decimal>() ?? 0m;
                    var deliveryDate = item["deliveryDate"]?.ToString() ?? "";

                    // SO 格式映射
                    worksheet.Cells[rowIndex, 1].Value = uploadNo;                      // UPLOAD NO
                    worksheet.Cells[rowIndex, 2].Value = lineNum;                       // LINE
                    worksheet.Cells[rowIndex, 3].Value = customerNo;                    // CUSTOMER NO.
                    worksheet.Cells[rowIndex, 4].Value = poNo;                          // PO NO.
                    worksheet.Cells[rowIndex, 5].Value = json["orderType"]?.ToString() ?? "";  // ORDER TYPE
                    worksheet.Cells[rowIndex, 6].Value = poDate;                        // DATE
                    worksheet.Cells[rowIndex, 7].Value = deliveryAddress;               // SHIP TO
                    worksheet.Cells[rowIndex, 8].Value = contact;                       // SHIP TO ATTN
                    worksheet.Cells[rowIndex, 9].Value = buyer;                         // BILL TO
                    worksheet.Cells[rowIndex, 10].Value = currency;                     // CURRENCYY
                    worksheet.Cells[rowIndex, 11].Value = json["priceBook"]?.ToString() ?? "";  // PRICE BOOK
                    worksheet.Cells[rowIndex, 12].Value = json["sales"]?.ToString() ?? "";      // SALES
                    worksheet.Cells[rowIndex, 13].Value = paymentTerm;                  // PAYMENT TERM
                    worksheet.Cells[rowIndex, 14].Value = json["warehouse"]?.ToString() ?? "";  // WH
                    worksheet.Cells[rowIndex, 15].Value = json["lineSet"]?.ToString() ?? "";    // LINE SET
                    worksheet.Cells[rowIndex, 16].Value = json["shippingMethod"]?.ToString() ?? ""; // SHIPPING METHOD
                    worksheet.Cells[rowIndex, 17].Value = json["fob"]?.ToString() ?? "";        // FOB
                    worksheet.Cells[rowIndex, 18].Value = poNo;                         // SHIPPING INSTRUCTIONS
                    worksheet.Cells[rowIndex, 19].Value = json["conversionType"]?.ToString() ?? ""; // CONVERSION TYPE
                    worksheet.Cells[rowIndex, 22].Value = itemNo;                       // P/N
                    worksheet.Cells[rowIndex, 23].Value = description;                  // ITEM
                    worksheet.Cells[rowIndex, 24].Value = qty;                          // QTY
                    worksheet.Cells[rowIndex, 25].Value = unitPrice;                    // PRICE
                    worksheet.Cells[rowIndex, 26].Value = poNo;                         // PO
                    worksheet.Cells[rowIndex, 27].Value = lineNum * 10;                 // PO LINE
                    worksheet.Cells[rowIndex, 28].Value = json["taxCode"]?.ToString() ?? "";    // TAX CODE
                    worksheet.Cells[rowIndex, 29].Value = poDate;                       // RD (1)
                    worksheet.Cells[rowIndex, 30].Value = deliveryDate;                 // RD (2)
                    worksheet.Cells[rowIndex, 31].Value = json["shipFrom"]?.ToString() ?? "";   // 出貨地
                    worksheet.Cells[rowIndex, 32].Value = json["dpa"]?.ToString() ?? "";        // DPA
                    worksheet.Cells[rowIndex, 33].Value = json["sub"]?.ToString() ?? "";        // SUB
                    worksheet.Cells[rowIndex, 34].Value = lineNum;                      // SHIP SET

                    // 格式化數字欄位
                    worksheet.Cells[rowIndex, 24].Style.Numberformat.Format = "0";
                    worksheet.Cells[rowIndex, 25].Style.Numberformat.Format = "#,##0.0000";

                    rowIndex++;
                    lineNum++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing SO item: {ex.Message}");
                }
            }

            AdjustWorksheet(worksheet, HeadersSO.Count);
            return workbook.GetAsByteArray();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to convert to SO format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 將採購單 JSON 轉換為兩階段 Excel 格式
    /// </summary>
    public static byte[] ConvertToTwoStage(string poJsonString, string orderName = "AUTO")
    {
        try
        {
            var json = JObject.Parse(poJsonString);
            var workbook = new ExcelPackage();
            var worksheet = workbook.Workbook.Worksheets.Add("兩階段");

            // 寫入標題列
            WriteHeaders(worksheet, HeadersTwoStage);

            // 提取採購單資料
            var poNo = json["poNo"]?.ToString() ?? json["purchaseOrder"]?.ToString() ?? "";
            var poDate = json["poDate"]?.ToString() ?? json["date"]?.ToString() ?? DateTime.Now.ToString("yyyy/MM/dd");
            var buyer = json["buyer"]?.ToString() ?? "";
            var items = json["items"] as JArray ?? new JArray();

            // 寫入行資料
            int rowIndex = 2;
            int lineNum = 1;
            foreach (var item in items)
            {
                try
                {
                    var itemNo = item["itemNo"]?.ToString() ?? "";
                    var qty = item["quantity"]?.Value<int>() ?? 0;

                    // 兩階段格式映射
                    worksheet.Cells[rowIndex, 1].Value = orderName;                     // 訂單名
                    worksheet.Cells[rowIndex, 2].Value = json["fromOrg"]?.ToString() ?? "";     // From Org
                    worksheet.Cells[rowIndex, 3].Value = json["toOrg"]?.ToString() ?? "";       // To Org
                    worksheet.Cells[rowIndex, 4].Value = json["category"]?.ToString() ?? "";    // 類別
                    worksheet.Cells[rowIndex, 5].Value = json["transactionType"]?.ToString() ?? ""; // Transaction Type
                    worksheet.Cells[rowIndex, 6].Value = json["shipCustomer"]?.ToString() ?? ""; // 發出客戶
                    worksheet.Cells[rowIndex, 7].Value = buyer;                         // 發出客戶代號
                    worksheet.Cells[rowIndex, 8].Value = json["endCustomer"]?.ToString() ?? ""; // EndCust發出客戶
                    worksheet.Cells[rowIndex, 9].Value = buyer;                         // EndCust發出客戶代號
                    worksheet.Cells[rowIndex, 10].Value = json["taxType"]?.ToString() ?? "";    // 稅or免稅
                    worksheet.Cells[rowIndex, 11].Value = json["shippingMode"]?.ToString() ?? ""; // 運送模式
                    worksheet.Cells[rowIndex, 12].Value = json["mawb"]?.ToString() ?? "";       // Mawb
                    worksheet.Cells[rowIndex, 13].Value = json["hawb"]?.ToString() ?? "";       // Hawb
                    worksheet.Cells[rowIndex, 14].Value = poNo;                         // 客戶料號
                    worksheet.Cells[rowIndex, 15].Value = json["quantityBatch"]?.ToString() ?? ""; // 數量批號
                    worksheet.Cells[rowIndex, 16].Value = json["wfNo"]?.ToString() ?? "";       // WF流程號
                    worksheet.Cells[rowIndex, 17].Value = json["shipVia"]?.ToString() ?? "";    // Ship Via
                    worksheet.Cells[rowIndex, 18].Value = json["operator"]?.ToString() ?? "";   // 客戶作業員
                    worksheet.Cells[rowIndex, 19].Value = lineNum;                      // Line Num
                    worksheet.Cells[rowIndex, 20].Value = itemNo;                       // From Item
                    worksheet.Cells[rowIndex, 21].Value = json["fromSubinventory"]?.ToString() ?? ""; // From Subinvnetory
                    worksheet.Cells[rowIndex, 22].Value = json["toSubinventory"]?.ToString() ?? "";   // To Subinventory
                    worksheet.Cells[rowIndex, 23].Value = qty;                          // Quantity
                    worksheet.Cells[rowIndex, 24].Value = json["poNumber"]?.ToString() ?? "";   // Po Number
                    worksheet.Cells[rowIndex, 25].Value = json["invoiceNumber"]?.ToString() ?? ""; // Invoice Number
                    worksheet.Cells[rowIndex, 26].Value = json["vendorRmaNum"]?.ToString() ?? ""; // Vendor RMA Num
                    worksheet.Cells[rowIndex, 27].Value = json["creditNumber"]?.ToString() ?? ""; // Credit Number
                    worksheet.Cells[rowIndex, 28].Value = json["batchNo"]?.ToString() ?? "";    // 批號批號
                    worksheet.Cells[rowIndex, 29].Value = json["remarks"]?.ToString() ?? "";    // 備註欄位
                    worksheet.Cells[rowIndex, 30].Value = json["checkSum"]?.ToString() ?? "";   // Check Sum
                    worksheet.Cells[rowIndex, 31].Value = json["country"]?.ToString() ?? "";    // 國家
                    worksheet.Cells[rowIndex, 32].Value = json["doNo"]?.ToString() ?? "";       // Do No 編號
                    worksheet.Cells[rowIndex, 33].Value = json["rmaNo"]?.ToString() ?? "";      // RMA No(Attribute1)

                    // 格式化數字欄位
                    worksheet.Cells[rowIndex, 23].Style.Numberformat.Format = "0";

                    rowIndex++;
                    lineNum++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing TwoStage item: {ex.Message}");
                }
            }

            AdjustWorksheet(worksheet, HeadersTwoStage.Count);
            return workbook.GetAsByteArray();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to convert to TwoStage format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 寫入標題列
    /// </summary>
    private static void WriteHeaders(ExcelWorksheet worksheet, List<string> headers)
    {
        for (int col = 0; col < headers.Count; col++)
        {
            var cell = worksheet.Cells[1, col + 1];
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Font.Size = 11;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(217, 217, 217));
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            cell.Style.WrapText = true;
        }
    }

    /// <summary>
    /// 調整工作表格式
    /// </summary>
    private static void AdjustWorksheet(ExcelWorksheet worksheet, int columnCount)
    {
        worksheet.Row(1).Height = 25;
        for (int col = 1; col <= columnCount; col++)
        {
            worksheet.Column(col).Width = 14;
        }
    }

    /// <summary>
    /// 將採購單 JSON 轉換為 Excel（根據格式選擇）
    /// </summary>
    public static byte[] ConvertToExcel(string poJsonString, string format = "CO", string paramValue = "AUTO")
    {
        format = format.ToUpper();

        return format switch
        {
            "SO" => ConvertToSO(poJsonString, paramValue),
            "TWOSTAGE" or "兩階段" or "2STAGE" => ConvertToTwoStage(poJsonString, paramValue),
            _ => ConvertToCO(poJsonString, paramValue) // 預設 CO
        };
    }
}
