#!/bin/bash

# Azure Document Intelligence 配置
ENDPOINT="https://document-intelligence-weikengpoc.cognitiveservices.azure.com/"
API_KEY="1ec7a0fa8e2a42e8b883bfdc32b95aab"
PDF_FILE="高盛達科技香港 INV.pdf"

# 將 PDF 轉換為 base64
PDF_BASE64=$(base64 < "$PDF_FILE" | tr -d '\n')

# 調用 Document Intelligence API
curl -s -X POST \
  "${ENDPOINT}documentintelligence:analyzeDocument?api-version=2024-02-29-preview" \
  -H "Ocp-Apim-Subscription-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d "{
    \"urlSource\": \"data:application/pdf;base64,$PDF_BASE64\"
  }" | jq '.' > ocr_result.json

echo "✅ OCR 結果已保存到 ocr_result.json"
echo "檔案大小: $(du -h ocr_result.json | cut -f1)"
