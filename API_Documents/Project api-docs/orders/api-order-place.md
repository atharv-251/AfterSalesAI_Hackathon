# API Documentation: Place Order

## Overview

This API submits a new spare parts order on behalf of a dealer or service partner. It is called after the user has built their order basket and confirmed the order. The request contains the order header information such as customer number, order type, delivery date and shipping condition, along with the list of parts and quantities to be ordered. On success the system creates the order in the backend and returns a confirmation message.

## Endpoint

```
POST /api/orderInput/placeOrder
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

The request body contains two main sections. The order header with delivery and account information, and the list of parts to be ordered.

#### Order Header Fields

| Field | Type | Required | Description |
|---|---|---|---|
| customerNumber | string | yes | The dealer or service partner customer number placing the order |
| orderType | string | yes | The type of order, for example standard or urgent |
| requestedDeliveryDate | timestamp | yes | The requested delivery date in Unix epoch milliseconds |
| shippingCondition | string | yes | The shipping condition code indicating the preferred delivery method |
| customerPurchaseOrderNumber | string | no | The customer's own purchase order reference number for their internal records |
| orderNote | string | no | A free text note or comment for the order |
| includeDeposit | string | no | Flag indicating whether deposit items should be included. X means yes |

#### Order Line Item Fields (repeated for each part)

| Field | Type | Required | Description |
|---|---|---|---|
| partNumber | string | yes | The unique part or material number to be ordered |
| partDescription | string | yes | The description of the part |
| quantity | number | yes | The quantity to be ordered |
| unitOfMeasure | string | yes | The unit of measure for the quantity, for example PC for piece |
| customerMaterialNumber | string | no | The customer's own internal material or part number for reference. Use NONE if not applicable |
| customerInfo | string | no | Additional reference information such as a job number or vehicle reference |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/orderInput/placeOrder" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "orderHeader": {
      "customerNumber": "10045",
      "orderType": "STANDARD",
      "requestedDeliveryDate": 1784053800000,
      "shippingCondition": "01",
      "customerPurchaseOrderNumber": "PO-2026-00912",
      "orderNote": "",
      "includeDeposit": "X"
    },
    "parts": [
      {
        "partNumber": "PART-00112-A",
        "partDescription": "Hex Shoulder Stud M8",
        "quantity": 2,
        "unitOfMeasure": "PC",
        "customerMaterialNumber": "NONE",
        "customerInfo": "45610 New vehicle preparation"
      },
      {
        "partNumber": "PART-00334-B",
        "partDescription": "Hex Collar Bolt M20",
        "quantity": 4,
        "unitOfMeasure": "PC",
        "customerMaterialNumber": "NONE",
        "customerInfo": "45610 New vehicle preparation"
      },
      {
        "partNumber": "PART-00567-C",
        "partDescription": "Pressure Sensor 16 BAR",
        "quantity": 1,
        "unitOfMeasure": "PC",
        "customerMaterialNumber": "C1217E",
        "customerInfo": "45610 New vehicle preparation"
      },
      {
        "partNumber": "PART-00789-D",
        "partDescription": "Trailer Coupling Assembly",
        "quantity": 1,
        "unitOfMeasure": "PC",
        "customerMaterialNumber": "NONE",
        "customerInfo": "45610 New vehicle preparation"
      },
      {
        "partNumber": "PART-00901-E",
        "partDescription": "LED Floodlight Headlight",
        "quantity": 1,
        "unitOfMeasure": "PC",
        "customerMaterialNumber": "DISPLAY",
        "customerInfo": "45610 New vehicle preparation"
      }
    ]
  }'
```

## Response

The response is a JSON array containing one or more messages returned by the backend after processing the order submission. Each message has a type indicating whether it is a success, warning, or error.

| Field | Type | Description |
|---|---|---|
| id | string | Internal message identifier. May be empty for system-generated messages |
| cmsUrl | string | URL to additional content or documentation related to the message. Empty if not applicable |
| messageNumber | string | The message number code from the backend system |
| messageType | string | Single character indicating the message type. S for success, W for warning, E for error |
| message | string | The human readable message text to be displayed to the user |

### Message Type Codes

| Type Code | Meaning |
|---|---|
| S | Success — the order was placed successfully |
| W | Warning — the order was placed but there is something the user should be aware of |
| E | Error — the order could not be placed, the message text explains the reason |

### Example Success Response

```json
[
  {
    "id": "",
    "cmsUrl": "",
    "messageNumber": "000",
    "messageType": "S",
    "message": "Thank you for your order. You can obtain details about the status of your order from order tracking."
  }
]
```

### Example Error Response

```json
[
  {
    "id": "",
    "cmsUrl": "",
    "messageNumber": "102",
    "messageType": "E",
    "message": "Order could not be placed. Credit limit exceeded for customer 10045. Please contact your account manager."
  }
]
```

### Example Warning Response

```json
[
  {
    "id": "",
    "cmsUrl": "",
    "messageNumber": "045",
    "messageType": "W",
    "message": "Order placed successfully. Note that part PART-00567-C has limited stock availability and may be subject to backlog."
  }
]
```

## Error Responses

If required fields in the order header or parts list are missing, the server returns a validation error before the order is submitted to the backend. If the customer account is blocked or has exceeded its credit limit, the backend returns an error message in the response array with message type E. General session and service availability errors follow the same pattern as described in the order list API documentation.

## Usage Notes for AI Agent

This endpoint is used to place a new order and is a write operation. The AI agent should use this endpoint only when the user explicitly confirms they want to place an order, not for informational queries. After a successful submission, the agent should relay the confirmation message to the user and suggest they use the order tracking API to monitor the order status. If the response contains a message type of E, the agent should clearly communicate the error reason to the user and suggest corrective action such as reducing the order quantity or contacting their account manager. The customer info field on each line item is a free text reference that dealers use to associate parts with a specific job or vehicle, and the agent should preserve this if provided by the user.
