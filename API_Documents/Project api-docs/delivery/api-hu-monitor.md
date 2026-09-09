# API Documentation: Handling Unit Monitor

## Overview

This API retrieves a list of handling units for a customer within a specified date range. Unlike the delivery handling unit info endpoint which retrieves packaging details for a single delivery line item, this endpoint provides a broader monitoring view across all handling units — allowing the customer to track shipments at the packaging level across multiple deliveries. It is used to display the Handling Unit Monitor screen.

## Endpoint

```
POST /api/humonitor/getHuMonitorList
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

The main filter criteria are provided in a header object. Additional optional filter lists allow narrowing results by specific handling unit identifiers, invoice numbers, transport numbers, or delivery document numbers.

| Field | Type | Required | Description |
|---|---|---|---|
| customerNumber | string | yes | The ship-to customer number used to fetch handling units for that account |
| dateFrom | timestamp | yes | Start of the date range in Unix epoch milliseconds |
| dateTo | timestamp | yes | End of the date range in Unix epoch milliseconds |
| maxResults | integer | no | Maximum number of records to return. Typical value is 1000 |
| handlingUnitIds | array | no | List of specific handling unit identifier strings to filter by. Pass empty array for no filter |
| invoiceNumbers | array | no | List of invoice number strings to filter by. Pass empty array for no filter |
| transportNumbers | array | no | List of transport or shipment number strings to filter by. Pass empty array for no filter |
| deliveryNumbers | array | no | List of delivery document number strings to filter by. Pass empty array for no filter |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/humonitor/getHuMonitorList" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "customerNumber": "10045",
    "dateFrom": 1780000000000,
    "dateTo": 1785000000000,
    "maxResults": 1000,
    "handlingUnitIds": [],
    "invoiceNumbers": [],
    "transportNumbers": [],
    "deliveryNumbers": []
  }'
```

## Response

The response contains handling unit header records. Each record represents one physical handling unit and includes its identifier, associated delivery and transport references, status, weight, and dispatch information.

## Error Responses

If the session is invalid, an authentication error is returned. If required fields are missing, a validation error is returned. All error responses include a message field.

## Usage Notes for AI Agent

Call this endpoint when the user asks about handling units, physical shipment packages, or wants to monitor the status of packed shipments across multiple deliveries. This is a monitoring-level endpoint and provides a broader view than the single-item handling unit info endpoint under `deliveryStatusMain`. If the user is asking about a specific delivery's packaging, use `getDeliveryHuInfo` instead. If the user wants an overview of all recent handling units or wants to search by transport number or invoice number, use this endpoint.
