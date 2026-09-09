# API Documentation: Validate Material for Parts Locator

## Overview

This API validates a material or part number and retrieves its basic information before performing a stock lookup across plants. It is the first step in the Parts Locator workflow. The response confirms whether the part number is valid and returns descriptive information about the part, including its description and any relevant validity dates. The agent should call this endpoint before calling the material stock endpoint to ensure the part number is valid.

## Endpoint

```
POST /api/partslocator/validateMaterial
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
| materialNumber | string | yes | The part or material number to validate. Accepts both internal and customer-facing part number formats. Maximum 35 characters |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/partslocator/validateMaterial" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "materialNumber": "PART-00451-A"
  }'
```

## Response

The response is a single object containing the validated material information.

| Field | Type | Description |
|---|---|---|
| materialNumber | string | The resolved internal material number |
| materialDescription | string | The description of the part |
| validToDate | string | The date until which this part is valid or available, formatted according to the user's date format preference. Empty if no expiry applies |
| unitOfMeasure | string | The base unit of measure for this material |

### Example Response

```json
{
  "materialNumber": "PART-00451-A",
  "materialDescription": "Brake Pad Set Front Axle",
  "validToDate": "12/31/2027",
  "unitOfMeasure": "EA"
}
```

## Error Responses

If the session is invalid, an authentication error is returned. If the material number does not exist in the system, an error response is returned with a message indicating the part was not found. All error responses include a message field.

## Usage Notes for AI Agent

Call this endpoint first whenever the user asks about stock availability for a specific part number. Use the validated `materialNumber` from the response when making the subsequent `getStockByPlant` call. If the response indicates the part is not found or invalid, inform the user before proceeding. This is a read-only lookup and does not require user confirmation.
