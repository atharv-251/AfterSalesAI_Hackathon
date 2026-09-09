# API Documentation: Parts Bill of Materials List

## Overview

This API retrieves the bill of materials (BOM) for a given part or material number. The BOM describes the component structure of a part — that is, which sub-parts or assemblies make up the requested material. This endpoint is used to display the Parts List screen, allowing users to explore the component hierarchy of a part.

## Endpoint

```
POST /api/partslist/getBomList
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
| materialNumber | string | yes | The part or material number for which the BOM should be retrieved. Maximum 18 characters |
| bomUsageType | string | yes | A single character code indicating the BOM usage type. This determines which type of BOM is retrieved, for example production or spare parts |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/partslist/getBomList" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "materialNumber": "PART-00451-A",
    "bomUsageType": "3"
  }'
```

## Response

The response contains the BOM structure for the requested material, including each component part number, description, required quantity, unit of measure, and its position within the assembly hierarchy.

| Field | Type | Description |
|---|---|---|
| componentMaterialNumber | string | The part or material number of the component |
| componentDescription | string | The description of the component part |
| requiredQuantity | number | The quantity of this component required in the assembly |
| unitOfMeasure | string | The unit of measure for the required quantity |
| itemNumber | string | The position number of this component within the BOM |
| itemCategory | string | The category code of the BOM item |

### Example Response

```json
[
  {
    "componentMaterialNumber": "PART-00452-B",
    "componentDescription": "Brake Disc Front",
    "requiredQuantity": 2,
    "unitOfMeasure": "EA",
    "itemNumber": "0010",
    "itemCategory": "L"
  },
  {
    "componentMaterialNumber": "PART-00453-C",
    "componentDescription": "Brake Pad Set",
    "requiredQuantity": 1,
    "unitOfMeasure": "ST",
    "itemNumber": "0020",
    "itemCategory": "L"
  }
]
```

## Error Responses

If the session is invalid, an authentication error is returned. If the material number is missing or invalid, a validation error is returned. All error responses include a message field.

## Usage Notes for AI Agent

Call this endpoint when the user asks about the components or sub-parts that make up a specific part, or wants to explore the assembly structure of a material. The `materialNumber` must be a valid part number known to the system. This is a read-only lookup endpoint and does not require user confirmation before calling.
