# API Documentation: Delivery Status Main List

## Overview

This API retrieves a list of delivery order headers for a customer within a specified date range. Each entry represents one delivery order at the header level, showing its overall delivery status, shipping condition, originating plant, and logistics center. This is the primary entry point for the Delivery Status module and is used to display the delivery overview screen.

## Endpoint

```
POST /api/deliverystatus/getDeliveryList
```

## Authentication

This endpoint requires an active session. The session is established after a successful login and is maintained via a session cookie. All requests must include the session cookie in the request header. Requests without a valid session will be rejected with an authentication error.

## Request

### Headers

| Header | Value |
|---|---|
| Content-Type | application/json |
| Accept | application/json |
| X-Requested-With | XMLHttpRequest |

### Request Body

The request body is a JSON object. The main filter criteria are provided in a nested header object along with optional lists for narrowing results by purchase order number, material number, or document number.

| Field | Type | Required | Description |
|---|---|---|---|
| customerNumber | string | yes | The ship-to customer number used to fetch deliveries for that account |
| shippingCondition | string | no | Filter by shipping condition code. Leave empty to retrieve all shipping conditions |
| deliveryStatus | string | no | Filter by delivery status code. Leave empty for all statuses. Use `A` for open only, `C` for completed only |
| dateFrom | timestamp | yes | Start of the document date range in Unix epoch milliseconds |
| dateTo | timestamp | yes | End of the document date range in Unix epoch milliseconds |
| maxResults | integer | no | Maximum number of records to return. Typical value is 1000 |
| customerOrderNumbers | array | no | List of customer purchase order number strings to filter by. Pass empty array for no filter |
| materialNumbers | array | no | List of material or part number strings to filter by. Pass empty array for no filter |
| deliveryNumbers | array | no | List of specific delivery document number strings to filter by. Pass empty array for no filter |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/deliverystatus/getDeliveryList" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "customerNumber": "10045",
    "shippingCondition": "",
    "deliveryStatus": "",
    "dateFrom": 1780000000000,
    "dateTo": 1785000000000,
    "maxResults": 1000,
    "customerOrderNumbers": [],
    "materialNumbers": [],
    "deliveryNumbers": []
  }'
```

## Response

The response is a JSON array where each element represents one delivery order header.

| Field | Type | Description |
|---|---|---|
| customerNumber | string | The ship-to customer number associated with the delivery |
| deliveryNumber | string | The unique delivery document number assigned by the system |
| documentDate | string | The date the delivery document was created, formatted as MM/DD/YYYY |
| goodsIssueDate | string | The date goods were physically dispatched. Empty for orders not yet shipped |
| plantCode | string | The code of the plant from which the goods are dispatched |
| plantName | string | The name of the dispatching plant |
| orderType | string | The type of order, for example Standard Order or Urgent Order |
| deliveryStatusCode | string | Single character code representing the delivery status |
| deliveryStatusText | string | Human readable description of the delivery status |
| shippingConditionText | string | Human readable description of the shipping condition, such as Standard or Urgent |
| shippingPointCode | string | The code of the logistics center or shipping point |
| shippingPointName | string | The full name of the logistics center or shipping point |
| deliveryNoteReference | string | A document management system reference for the delivery note. Empty for open orders |

### Delivery Status Codes

| Status Code | Status Text | Meaning |
|---|---|---|
| A | Open | The delivery order has been created but goods have not yet been dispatched |
| C | Completed | Goods have been issued and the delivery is fully processed |

### Example Response

```json
[
  {
    "customerNumber": "10045",
    "deliveryNumber": "DEL-001",
    "documentDate": "06/16/2026",
    "goodsIssueDate": "",
    "plantCode": "P001",
    "plantName": "Central Warehouse",
    "orderType": "Standard Order",
    "deliveryStatusCode": "A",
    "deliveryStatusText": "Open",
    "shippingConditionText": "Standard",
    "shippingPointCode": "LC01",
    "shippingPointName": "Logistics Center A",
    "deliveryNoteReference": ""
  },
  {
    "customerNumber": "10045",
    "deliveryNumber": "DEL-002",
    "documentDate": "07/07/2026",
    "goodsIssueDate": "07/07/2026",
    "plantCode": "P001",
    "plantName": "Central Warehouse",
    "orderType": "Urgent Order",
    "deliveryStatusCode": "C",
    "deliveryStatusText": "Completed",
    "shippingConditionText": "Urgent",
    "shippingPointCode": "LC01",
    "shippingPointName": "Logistics Center A",
    "deliveryNoteReference": "DOC-REF-00123"
  }
]
```

## Error Responses

If the session has expired or is invalid, the server returns an authentication error. If required fields such as the customer number or date range are missing, the server returns a validation error. If the backend system is temporarily unavailable, a service unavailable error is returned. All error responses include a message field describing the reason for the failure.

## Usage Notes for AI Agent

Call this endpoint when the user asks about delivery status, shipment status, or wants to see which orders have been shipped or are still open. Use the `deliveryNumber` values from the response to make follow-up calls to the delivery item endpoint for line-item details. When the user asks about a specific delivery by their own purchase order reference, pass it in the `customerOrderNumbers` array. When the user asks about a specific part, pass it in the `materialNumbers` array. The `goodsIssueDate` being empty is a reliable indicator that an order has not yet been shipped.
