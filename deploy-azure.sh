#!/bin/bash

# Azure Function App 部署腳本
# 用於部署威健 PDF OCR Function App

# 設定變數
RESOURCE_GROUP="rg-wk-pdf-ocr"
LOCATION="East Asia"
TIMESTAMP=$(date +%s)
FUNCTION_APP_NAME="func-wk-pdf-ocr-${TIMESTAMP}"
STORAGE_ACCOUNT_NAME="stwkpdfocr${TIMESTAMP}"

echo "開始部署 Azure Function App..."
echo "資源群組: $RESOURCE_GROUP"
echo "位置: $LOCATION"
echo "Function App 名稱: $FUNCTION_APP_NAME"
echo "儲存體帳戶名稱: $STORAGE_ACCOUNT_NAME"

# 檢查是否已登入 Azure
echo "檢查 Azure 登入狀態..."
az account show > /dev/null 2>&1
if [ $? -ne 0 ]; then
    echo "請先登入 Azure: az login"
    exit 1
fi

# 建立資源群組
echo "建立資源群組..."
az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION"

if [ $? -ne 0 ]; then
    echo "建立資源群組失敗"
    exit 1
fi

# 建立儲存體帳戶
echo "建立儲存體帳戶..."
az storage account create \
  --name "$STORAGE_ACCOUNT_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --sku Standard_LRS \
  --kind StorageV2

if [ $? -ne 0 ]; then
    echo "建立儲存體帳戶失敗"
    exit 1
fi

# 建立 Function App
echo "建立 Function App..."
az functionapp create \
  --resource-group "$RESOURCE_GROUP" \
  --consumption-plan-location "$LOCATION" \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4 \
  --name "$FUNCTION_APP_NAME" \
  --storage-account "$STORAGE_ACCOUNT_NAME" \
  --os-type Windows

if [ $? -ne 0 ]; then
    echo "建立 Function App 失敗"
    exit 1
fi

# 設定環境變數（Document Intelligence）
echo "設定環境變數..."
az functionapp config appsettings set \
  --name "$FUNCTION_APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --settings \
    "AZ_DOCUMENT_INTELLIGENCE_ENDPOINT=https://wk-doc-intelligence.cognitiveservices.azure.com/" \
    "AZ_DOCUMENT_INTELLIGENCE_KEY=$DOCUMENT_INTELLIGENCE_KEY" \
    "AZ_OCR_MAX_FILE_BYTES=20000000" \
    "AZ_OCR_POLL_MAX_ATTEMPTS=40" \
    "AZ_OCR_POLL_INITIAL_DELAY_MS=500"

if [ $? -ne 0 ]; then
    echo "設定環境變數失敗"
    exit 1
fi

# 設定 CORS
echo "設定 CORS..."
az functionapp cors add \
  --name "$FUNCTION_APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --allowed-origins "*"

# 建置專案
echo "建置專案..."
dotnet publish --configuration Release --output ./publish

if [ $? -ne 0 ]; then
    echo "建置專案失敗"
    exit 1
fi

echo "部署完成！"
echo "Function App 名稱: $FUNCTION_APP_NAME"
echo "Function App URL: https://${FUNCTION_APP_NAME}.azurewebsites.net"
echo ""
echo "取得 Function 金鑰請執行："
echo "az functionapp keys list --name $FUNCTION_APP_NAME --resource-group $RESOURCE_GROUP"
echo ""
echo "部署程式碼請執行："
echo "func azure functionapp publish $FUNCTION_APP_NAME"