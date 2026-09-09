# API Documentation: Billing List Item

## Overview

This API retrieves the line items for one or more billing documents. Where the billing list head endpoint returns one row per billing document header, this endpoint returns one row per billed part within each document. It is used to display the detailed contents of a billing document when the user drills down from the billing overview screen.

## Endpoint

```
POST /api/billinglist/getBillingItemList
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

The request body contains a list of billing document numbers for which line items should be retrieved.

| Field | Type | Required | Description |
|---|---|---|---|
| billingDocumentNumbers | array | yes | List of billing document number strings to retrieve line items for |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/billinglist/getBillingItemList" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "billingDocumentNumbers": ["BILL-001", "BILL-002"]
  }'
```

## Response

The response is a JSON array where each element represents one line item within a billing document.

| Field | Type | Description |
|---|---|---|
| billingDocumentNumber | string | The billing document number this line item belongs to |
| lineItemNumber | string | The line item position number within the billing document |
| materialNumber | string | The part or material number that was billed |
| materialDescription | string | The description of the billed part |
| billedQuantity | number | The quantity billed for this line item |
| unitOfMeasure | string | The unit of measure for the quantity, for example EA or PC |
| listPrice | number | The list price per unit before any discounts |
| netPrice | number | The net price per unit after discounts |
| netValue | number | The total net value for this line item |
| currency | string | The currency code for all monetary values in this line item |
| referenceDeliveryNumber | string | The delivery document number this billing line item references |
| referenceDeliveryLineItem | string | The line item number in the referenced delivery document |
| openQuantity | number | The quantity that has been billed but not yet fully settled |

### Example Response

```json
[
  {
    "billingDocumentNumber": "BILL-001",
    "lineItemNumber": "000010",
    "materialNumber": "PART-00782-B",
    "materialDescription": "Oil Filter",
    "billedQuantity": 10,
    "unitOfMeasure": "EA",
    "listPrice": 132.50,
    "netPrice": 120.56,
    "netValue": 1205.60,
    "currency": "EUR",
    "referenceDeliveryNumber": "DEL-002",
    "referenceDeliveryLineItem": "000010",
    "openQuantity": 0
  },
  {
    "billingDocumentNumber": "BILL-002",
    "lineItemNumber": "000010",
    "materialNumber": "PART-00451-A",
    "materialDescription": "Brake Pad Set Front Axle",
    "billedQuantity": 4,
    "unitOfMeasure": "EA",
    "listPrice": 95.20,
    "netPrice": 85.70,
    "netValue": -342.80,
    "currency": "EUR",
    "referenceDeliveryNumber": "DEL-001",
    "referenceDeliveryLineItem": "000010",
    "openQuantity": 0
  }
]
```

## Error Responses

If the session is invalid, an authentication error is returned. If the billing document numbers list is empty or missing, a validation error is returned. All error responses include a message field.

## Usage Notes for AI Agent

Call this endpoint after `getBillingList` when the user wants to see the individual parts within a specific billing document. Pass the `billingDocumentNumber` values obtained from the billing head response. Multiple billing document numbers can be passed in a single call. The `lineItemNumber` from this response is required as input when calling the billing detail endpoint for reference document information. The `referenceDeliveryNumber` field links each billing line back to the originating delivery document.
