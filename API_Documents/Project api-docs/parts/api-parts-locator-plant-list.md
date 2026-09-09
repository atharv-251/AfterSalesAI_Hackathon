# API Documentation: User Plant List

## Overview

This API retrieves the list of plants or warehouses available to the current user for stock lookup purposes. It is used in the Parts Locator module to populate the plant selection list before performing a stock query. The response includes all plants the user has access to, with an option to return only the user's saved favourite plants.

## Endpoint

```
POST /api/partslocator/getPlantList
```

## Authentication

This endpoint requires an active session maintained via a session cookie. Requests without a valid session will be rejected with an authentication error.

## Request

### Headers

| Header | Value |
|---|---|
| Content-Type | application/json |
| Accept | application/json |
| X-Requested-With | XMLHttpRequest |

### Request Body

| Field | Type | Required | Description |
|---|---|---|---|
| onlyFavourites | string | yes | Pass `X` to return only the user's saved favourite plants. Pass an empty string to return all available plants |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/partslocator/getPlantList" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "onlyFavourites": ""
  }'
```

## Response

The response is a JSON array where each element represents one plant available to the user.

| Field | Type | Description |
|---|---|---|
| plantCode | string | The unique code identifying the plant or warehouse |
| plantName | string | The human readable name of the plant |
| isFavourite | boolean | Indicates whether this plant is saved as a favourite by the current user |

### Example Response

```json
[
  {
    "plantCode": "WH01",
    "plantName": "Central Warehouse North",
    "isFavourite": true
  },
  {
    "plantCode": "WH02",
    "plantName": "Regional Warehouse South",
    "isFavourite": false
  },
  {
    "plantCode": "WH03",
    "plantName": "Express Parts Depot",
    "isFavourite": true
  }
]
```

## Error Responses

If the session is invalid, an authentication error is returned. All error responses include a message field.

## Usage Notes for AI Agent

Call this endpoint when setting up a stock query in the Parts Locator to determine which plants are available for selection. The `plantCode` values from this response are used as input to the `getStockByPlant` endpoint. If the user has previously saved favourite plants, passing `X` in `onlyFavourites` will return a shorter, more relevant list. This is a read-only lookup and does not require user confirmation.
