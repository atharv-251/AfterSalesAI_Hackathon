# API Documentation: Get Claim Types

## Overview

This API retrieves the list of available claim types, order types, and rejection reasons that can be used when filtering the claim monitor list or when submitting a new claim. It is typically called once when the claim screens are loaded to populate the filter dropdowns. For an AI agent, this endpoint is useful for answering questions about what types of claims are available and what rejection reason codes mean.

## Endpoint

```
POST /api/claimtracking/getClaimTypes
```

## Authentication

This endpoint requires an active session cookie. See the order list API documentation for authentication details.

## Request

### Headers

| Header | Value |
|---|---|
| Content-Type | application/json |
| Accept | application/json |
| X-Requested-With | XMLHttpRequest |

### Request Body

The request body requires only the customer number. Language and user identity are injected server-side from the session.

| Field | Type | Required | Description |
|---|---|---|---|
| customerNumber | string | yes | The dealer or service partner customer number |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/claimtracking/getClaimTypes" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "customerNumber": "10045"
  }'
```

## Response

The response is a JSON object containing three arrays. Each array contains the available values for one of the filter dimensions used in claim processing.

| Field | Type | Description |
|---|---|---|
| claimReasons | array | List of available claim reason or complaint type codes with their descriptions |
| orderTypes | array | List of order type codes applicable to claims with their descriptions |
| rejectionReasons | array | List of rejection reason codes with their descriptions |

Each item in the arrays contains:

| Field | Type | Description |
|---|---|---|
| code | string | The internal code value used in API requests and responses |
| description | string | The human readable label for this code |

### Example Response

```json
{
  "claimReasons": [
    { "code": "WR01", "description": "Warranty Claim" },
    { "code": "DM02", "description": "Damage Claim" },
    { "code": "QD03", "description": "Quantity Discrepancy" },
    { "code": "QR04", "description": "Quarterly Return" },
    { "code": "YR05", "description": "Yearly Return" }
  ],
  "orderTypes": [
    { "code": "ZRET", "description": "Return Order" },
    { "code": "ZWAR", "description": "Warranty Order" },
    { "code": "ZQRT", "description": "Quarterly Return Order" },
    { "code": "ZYRT", "description": "Yearly Return Order" }
  ],
  "rejectionReasons": [
    { "code": "EX01", "description": "Part outside warranty period" },
    { "code": "EX02", "description": "Damage caused by incorrect installation" },
    { "code": "EX03", "description": "Normal wear and tear" },
    { "code": "EX04", "description": "Insufficient supporting documentation" },
    { "code": "EX05", "description": "Claim submitted after deadline" },
    { "code": "EX06", "description": "Part not eligible for return program" },
    { "code": "EX07", "description": "Part returned in damaged or used condition" }
  ]
}
```

## Usage Notes for AI Agent

This endpoint should be called when a user asks what types of claims they can raise, or when they want to understand what a specific rejection reason code means. The response from this endpoint can be cached as the values change infrequently. When a user asks why their claim was rejected and the claim monitor list returns a rejection reason code, the description from this endpoint can be used to provide a plain language explanation to the user.
