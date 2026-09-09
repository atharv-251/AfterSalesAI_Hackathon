# API Documentation: Get Order List

## Overview

This API retrieves a list of orders placed by a dealer or service partner within a specified date range. It is the primary endpoint used to display the order tracking overview screen. Each entry in the response represents one order header with its current status, total value, and key dates.

## Endpoint

```
POST /api/orderTracking/getOrderList
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

The request body is a JSON object. The most important fields for filtering the order list are the customer number, the date range, and optional filters for order status and shipping condition.

| Field | Type | Required | Description |
|---|---|---|---|
| customerNumber | string | yes | The dealer or service partner customer number used to fetch orders belonging to that account |
| orderStatus | string | no | Filter by order status code. Leave empty to retrieve all statuses |
| shippingCondition | string | no | Filter by shipping or delivery condition code. Leave empty for all |
| orderType | string | no | Filter by order type such as standard or urgent. Leave empty for all |
| dateFrom | timestamp | yes | Start of the date range in Unix epoch milliseconds |
| dateTo | timestamp | yes | End of the date range in Unix epoch milliseconds |
| customerOrderNumbers | array | no | List of specific customer purchase order numbers to filter by. Pass empty array for no filter |
| materialNumbers | array | no | List of specific part or material numbers to filter by. Pass empty array for no filter |
| orderNumbers | array | no | List of specific internal order numbers to filter by. Pass empty array for no filter |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/orderTracking/getOrderList" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "is_HEAD": {
      "customerNumber": "10045",
      "orderStatus": "",
      "shippingCondition": "",
      "orderType": "",
      "dateFrom": 1780000000000,
      "dateTo": 1785000000000
    },
    "customerOrderNumbers": [],
    "materialNumbers": [],
    "orderNumbers": []
  }'
```

## Response

The response is a JSON array where each element represents one order. Only the most relevant fields for an AI agent are described below.

| Field | Type | Description |
|---|---|---|
| customerNumber | string | The customer number associated with the order |
| orderNumber | string | The unique internal order number assigned by the system |
| orderDate | string | The date the order was placed, formatted as MM/DD/YYYY |
| shippingCondition | string | The shipping condition code indicating the delivery method |
| requestedDeliveryDate | string | The requested delivery date provided at the time of ordering |
| customerPurchaseOrderNumber | string | The customer's own purchase order reference number |
| orderCreationTime | string | The time the order was created in HH:MM:SS format |
| netValue | number | The net value of the order before any surcharges |
| surchargeValue | number | Any additional surcharges applied to the order |
| totalValue | number | The total order value including surcharges |
| currency | string | The currency code for all monetary values in this order |
| orderType | string | The type of order for example Standard Order or Urgent Order |
| shippingConditionText | string | Human readable description of the shipping condition |
| creditCheckStatus | string | The result of the credit check performed on the order |
| overallStatus | string | Single character code representing the overall order status |
| overallStatusText | string | Human readable description of the overall order status |

### Overall Order Status Codes

| Status Code | Status Text | Meaning |
|---|---|---|
| A | Open | The order has been received and is awaiting processing |
| B | In Progress | The order is currently being picked or processed at the warehouse |
| C | Completed | All items in the order have been delivered and confirmed |
| D | Partially Completed | Some items have been delivered, others are still outstanding |

### Example Response

```json
[
  {
    "customerNumber": "10045",
    "orderNumber": "4000123456",
    "orderDate": "06/24/2026",
    "shippingCondition": "01",
    "requestedDeliveryDate": "06/25/2026",
    "customerPurchaseOrderNumber": "PO-2026-00891",
    "orderCreationTime": "09:15:42",
    "netValue": 342.80,
    "surchargeValue": 0.00,
    "totalValue": 342.80,
    "currency": "EUR",
    "orderType": "Standard Order",
    "shippingConditionText": "Standard",
    "creditCheckStatus": "Not Performed",
    "overallStatus": "A",
    "overallStatusText": "Open"
  },
  {
    "customerNumber": "10045",
    "orderNumber": "4000123389",
    "orderDate": "06/20/2026",
    "shippingCondition": "02",
    "requestedDeliveryDate": "06/21/2026",
    "customerPurchaseOrderNumber": "PO-2026-00874",
    "orderCreationTime": "14:30:05",
    "netValue": 1205.60,
    "surchargeValue": 18.50,
    "totalValue": 1224.10,
    "currency": "EUR",
    "orderType": "Urgent Order",
    "shippingConditionText": "Express",
    "creditCheckStatus": "Approved",
    "overallStatus": "C",
    "overallStatusText": "Completed"
  }
]
```

## Error Responses

If the session has expired or is invalid, the server returns an authentication error and the user must log in again. If the request body is missing required fields such as the customer number or date range, the server returns a validation error. If the backend system is temporarily unavailable, the server returns a service unavailable error. In all error cases the response body contains a message field describing the reason for the failure.

## Usage Notes for AI Agent

When a user asks about their orders, this endpoint should be called with the user's customer number and a relevant date range. If the user asks about recent orders, a date range covering the last 30 to 90 days is a reasonable default. The overall status text field is the most useful field for answering questions like "what is the status of my orders". The total value and currency fields are useful for answering questions about order spend. If the user asks about a specific order by their own purchase order number, the customer purchase order number field can be used to identify the relevant order in the response.
