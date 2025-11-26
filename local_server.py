#!/usr/bin/env python3
"""
簡單 HTTP 伺服器 - 處理 Excel 轉換請求
用於本地開發測試（無需 Azure Functions）
"""

import json
import sys
import os
from http.server import HTTPServer, SimpleHTTPRequestHandler
from urllib.parse import urlparse
import subprocess
import tempfile

class ExcelConversionHandler(SimpleHTTPRequestHandler):
    def do_POST(self):
        """處理 POST 請求"""
        if self.path == '/api/convert-invoice-to-excel':
            try:
                # 讀取請求
                content_length = int(self.headers.get('Content-Length', 0))
                body = self.rfile.read(content_length).decode('utf-8')
                request_data = json.loads(body)
                
                # 驗證必要欄位
                if not request_data.get('ocrJson'):
                    return self.send_error_response(400, 'OCR JSON is required')
                
                if not request_data.get('format') or request_data['format'] not in ['406', '407']:
                    return self.send_error_response(400, "Format must be '406' or '407'")
                
                # 呼叫 C# 轉換程式
                return self.convert_to_excel(request_data)
                
            except json.JSONDecodeError:
                return self.send_error_response(400, 'Invalid JSON format')
            except Exception as e:
                return self.send_error_response(500, f'Server error: {str(e)}')
        else:
            # 處理靜態文件
            return super().do_GET()
    
    def convert_to_excel(self, request_data):
        """呼叫 C# DLL 進行轉換"""
        try:
            ocr_json = request_data.get('ocrJson')
            format_type = request_data.get('format', '406')
            param_value = request_data.get('paramValue', 'AUTO')
            
            # 建立暫時檔案用來存儲 JSON
            with tempfile.NamedTemporaryFile(mode='w', suffix='.json', delete=False) as tmp_json:
                tmp_json.write(ocr_json)
                json_file = tmp_json.name
            
            # 呼叫 C# 程式
            # 注意：這裡需要你先編譯 C# 專案
            exe_path = '/Users/chentungching/Documents/精誠軟體服務/威健/CODE/bin/Debug/net8.0/CODE'
            
            # 使用 dotnet 直接執行
            cmd = [
                'dotnet',
                f'{exe_path}.dll',
                'convert',
                '--format', format_type,
                '--param', param_value,
                '--input', json_file
            ]
            
            # 實際上，我們應該使用 HTTP 呼叫到本地 Functions host
            # 改用直接 HTTP 到 localhost:7071
            return self.call_local_functions(ocr_json, format_type, param_value)
            
        except Exception as e:
            return self.send_error_response(500, f'Conversion failed: {str(e)}')
    
    def call_local_functions(self, ocr_json, format_type, param_value):
        """呼叫本地 Azure Functions host"""
        try:
            import urllib.request
            
            # 準備請求資料
            request_body = {
                'ocrJson': ocr_json,
                'format': format_type,
                'paramValue': param_value
            }
            
            # 發送請求到本地 Functions
            req = urllib.request.Request(
                'http://localhost:7071/api/convert-invoice-to-excel',
                data=json.dumps(request_body).encode('utf-8'),
                headers={'Content-Type': 'application/json'},
                method='POST'
            )
            
            with urllib.request.urlopen(req, timeout=30) as response:
                excel_data = response.read()
                
                # 發送 Excel 檔案到客戶端
                self.send_response(200)
                self.send_header('Content-Type', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
                self.send_header('Content-Disposition', 
                    f"attachment; filename=Invoice_{format_type}INF_{self.get_timestamp()}.xlsx")
                self.end_headers()
                self.wfile.write(excel_data)
                
        except urllib.error.URLError as e:
            return self.send_error_response(503, 
                'Cannot connect to local Functions host on localhost:7071. '
                'Make sure Functions are running: func host start')
        except Exception as e:
            return self.send_error_response(500, f'Error: {str(e)}')
    
    def send_error_response(self, status_code, message):
        """發送錯誤回應"""
        self.send_response(status_code)
        self.send_header('Content-Type', 'application/json')
        self.end_headers()
        error_response = json.dumps({'error': message})
        self.wfile.write(error_response.encode('utf-8'))
    
    def do_OPTIONS(self):
        """處理 CORS OPTIONS 請求"""
        self.send_response(200)
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type')
        self.end_headers()
    
    def end_headers(self):
        """添加 CORS 頭"""
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type')
        super().end_headers()
    
    @staticmethod
    def get_timestamp():
        """取得時間戳"""
        from datetime import datetime
        return datetime.now().strftime('%Y%m%d%H%M%S')

def main():
    """啟動伺服器"""
    port = 8000
    handler = ExcelConversionHandler
    
    try:
        server = HTTPServer(('localhost', port), handler)
        print(f'🚀 HTTP 伺服器已啟動: http://localhost:{port}')
        print(f'📄 開啟: http://localhost:{port}/invoice_format_converter.html')
        print(f'⚠️  確保 Azure Functions 也在執行: func host start (另一個終端)')
        print(f'按 Ctrl+C 停止伺服器\n')
        server.serve_forever()
    except KeyboardInterrupt:
        print('\n👋 伺服器已停止')
        sys.exit(0)
    except Exception as e:
        print(f'❌ 錯誤: {e}')
        sys.exit(1)

if __name__ == '__main__':
    main()
