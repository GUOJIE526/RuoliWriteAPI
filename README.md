# 書法作品展示後端 Web API

本專案提供書法作品管理後台的 Web API，包括圖片分頁查詢與瀏覽次數統計等功能。  
使用 ASP.NET Core Web API + Entity Framework Core 開發，提供 RESTful API 介面，方便前端 Nuxt.js / Vue.js 對接使用。

## 🔧 開發技術

- ASP.NET Core 8
- Entity Framework Core
- MSSQL (使用 GUID 為主鍵)
- Swagger UI (API 測試介面)

---

## 🚀 API 端點

### 📄 1. 分頁取得書法作品

**GET** `/api/images`

| 參數        | 類型 | 說明         | 預設值 |
|-------------|------|--------------|--------|
| `pageNumber` | int  | 第幾頁       | 1      |
| `pageSize`   | int  | 每頁幾筆資料 | 10     |

#### ✅ 回應格式：

```json
{
  "items": [
    {
      "artworkId": "guid-id",
      "title": "作品名稱",
      "createdYear": 2021,
      "imageUrl": "/Images/xxx.jpg",
      "description": "簡介",
      "dimensions": "60x90cm",
      "material": "水墨紙",
      "views": 123
    }
  ],
  "totalCount": 38,
  "pageNumber": 1,
  "pageSize": 10
}
```

👤 Author
Developed by Jacob Hong
📧 Contact: hungkaojay@gmail.com
