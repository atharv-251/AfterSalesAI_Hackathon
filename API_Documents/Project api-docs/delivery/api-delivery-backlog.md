# API Documentation: Delivery Backlog List

## Overview

This API retrieves the delivery backlog for a customer — that is, ordered items that have not yet been fully delivered. Each entry in the response represents an order or line item that is outstanding and pending fulfillment. This endpoint is used to display the Delivery Backlog screen, which gives the customer visibility into what they are still waiting to receive.

## Endpoint

```
POST /api/deliverybacklog/getBacklogList
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

The request body follows the same structure as the delivery status main endpoint. The main filter criteria are provided in a header object along with optional lists for narrowing results.

| Field | Type | Required | Description |
|---|---|---|---|
| customerNumber | string | yes | The ship-to customer number used to fetch the backlog for that account |
| dateFrom | timestamp | yes | Start of the date range in Unix epoch milliseconds |
| dateTo | timestamp | yes | End of the date range in Unix epoch milliseconds |
| maxResults | integer | no | Maximum number of records to return. Typical value is 1000 |
| customerOrderNumbers | array | no | List of customer purchase order number strings to filter by. Pass empty array for no filter |
| materialNumbers | array | no | List of material or part number strings to filter by. Pass empty array for no filter |
| deliveryNumbers | array | no | List of specific delivery document number strings to filter by. Pass empty array for no filter |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/deliverybacklog/getBacklogList" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "customerNumber": "10045",
    "dateFrom": 1780000000000,
    "dateTo": 1785000000000,
    "maxResults": 1000,
    "customerOrderNumbers": [],
    "materialNumbers": [],
    "deliveryNumbers": []
  }'
```

## Response

The response is a JSON array where each element represents a backlogged delivery item that is still outstanding.

| Field | Type | Description |
|---|---|---|
| customerNumber | string | The ship-to customer number |
| deliveryNumber | string | The delivery document number |
| documentDate | string | The date the delivery document was created, formatted as MM/DD/YYYY |
| plantCode | string | The code of the plant responsible for fulfillment |
| plantName | string | The name of the fulfilling plant |
| orderType | string | The type of order, for example Standard Order or Urgent Order |
| deliveryStatusCode | string | Single character code representing the current delivery status |
| deliveryStatusText | string | Human readable description of the delivery status |
| shippingConditionText | string | Human readable description of the shipping condition |
| shippingPointCode | string | The code of the logistics center or shipping point |
| shippingPointName | string | The full name of the logistics center or shipping point |

### Example Response

```json
[
  {
    "customerNumber": "10045",
    "deliveryNumber": "DEL-003",
    "documentDate": "06/10/2026",
    "plantCode": "P001",
    "plantName": "Central Warehouse",
    "orderType": "Standard Order",
    "deliveryStatusCode": "A",
    "deliveryStatusText": "Open",
    "shippingConditionText": "Standard",
    "shippingPointCode": "LC01",
    "shippingPointName": "Logistics Center A"
  }
]
```

## Error Responses

If the session is invalid, an authentication error is returned. If required fields are missing, a validation error is returned. All error responses include a message field.

## Usage Notes for AI Agent

Call this endpoint when the user asks what deliveries are still pending, what they are waiting for, or what is in their backlog. This endpoint is focused on outstanding items only — it is not used to check completed deliveries. If the user wants to see both open and completed deliveries, use the `getDeliveryList` endpoint instead.
