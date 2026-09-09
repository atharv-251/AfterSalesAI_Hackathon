# API Documentation: Material Stock by Plant

## Overview

This API retrieves the current stock levels for a specific part across one or more selected plants or warehouses. It is the core lookup endpoint of the Parts Locator module, showing the user where a part is available and in what quantity. The agent should call this endpoint after validating the material number and obtaining the list of plants the user wants to check.

## Endpoint

```
POST /api/partslocator/getStockByPlant
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

The request body contains a list of plant and material combinations to check stock for.

| Field | Type | Required | Description |
|---|---|---|---|
| plants | array | yes | List of plant stock query objects. Each object specifies the plant code and material number to check |

Each item in the `plants` array contains:

| Field | Type | Required | Description |
|---|---|---|---|
| plantCode | string | yes | The plant or warehouse code to check stock at |
| materialNumber | string | yes | The part or material number to check stock for |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/partslocator/getStockByPlant" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "plants": [
      { "plantCode": "WH01", "materialNumber": "PART-00451-A" },
      { "plantCode": "WH02", "materialNumber": "PART-00451-A" },
      { "plantCode": "WH03", "materialNumber": "PART-00451-A" }
    ]
  }'
```

## Response

The response is a JSON array where each element represents the stock situation for the requested part at one plant.

| Field | Type | Description |
|---|---|---|
| plantCode | string | The plant or warehouse code |
| plantName | string | The human readable name of the plant |
| materialNumber | string | The part or material number |
| materialDescription | string | The description of the part |
| availableStock | number | The quantity currently available in unrestricted stock |
| unitOfMeasure | string | The unit of measure for the stock quantity |
| stockStatus | string | A short indicator of the stock situation, for example Available or Out of Stock |

### Example Response

```json
[
  {
    "plantCode": "WH01",
    "plantName": "Central Warehouse North",
    "materialNumber": "PART-00451-A",
    "materialDescription": "Brake Pad Set Front Axle",
    "availableStock": 48,
    "unitOfMeasure": "EA",
    "stockStatus": "Available"
  },
  {
    "plantCode": "WH02",
    "plantName": "Regional Warehouse South",
    "materialNumber": "PART-00451-A",
    "materialDescription": "Brake Pad Set Front Axle",
    "availableStock": 0,
    "unitOfMeasure": "EA",
    "stockStatus": "Out of Stock"
  },
  {
    "plantCode": "WH03",
    "plantName": "Express Parts Depot",
    "materialNumber": "PART-00451-A",
    "materialDescription": "Brake Pad Set Front Axle",
    "availableStock": 12,
    "unitOfMeasure": "EA",
    "stockStatus": "Available"
  }
]
```

## Error Responses

If the session is invalid, an authentication error is returned. If the plants list is empty or a plant code is invalid, a validation error is returned. All error responses include a message field.

## Usage Notes for AI Agent

Call this endpoint when the user asks about stock availability or where a specific part can be found. Always call `validateMaterial` first to confirm the part number is valid, and `getPlantList` to obtain the available plant codes. Pass all relevant plants in a single request rather than making multiple calls. This is a read-only lookup and does not require user confirmation.
