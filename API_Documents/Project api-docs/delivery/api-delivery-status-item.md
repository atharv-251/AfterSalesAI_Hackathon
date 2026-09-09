# API Documentation: Delivery Status Item List

## Overview

This API retrieves the line items for one or more delivery orders. Where the delivery status main endpoint returns one row per order header, this endpoint returns one row per ordered part within each delivery. It is used to display the detailed contents of a delivery when the user drills down from the delivery overview screen.

## Endpoint

```
POST /api/deliverystatus/getDeliveryItemList
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

The request body contains a list of delivery document numbers for which line items should be retrieved.

| Field | Type | Required | Description |
|---|---|---|---|
| deliveryNumbers | array | yes | List of delivery document number strings to retrieve line items for |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/deliverystatus/getDeliveryItemList" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "deliveryNumbers": ["DEL-001", "DEL-002"]
  }'
```

## Response

The response is a JSON array where each element represents one line item within a delivery order.

| Field | Type | Description |
|---|---|---|
| deliveryNumber | string | The delivery document number this line item belongs to |
| lineItemNumber | string | The line item position number within the delivery |
| materialNumber | string | The part or material number |
| materialDescription | string | The description of the part |
| orderedQuantity | number | The quantity ordered for this line item |
| unitOfMeasure | string | The unit of measure for the quantity, for example EA or PC |
| deliveryStatusCode | string | Single character code representing the delivery status of this line item |
| deliveryStatusText | string | Human readable description of the line item delivery status |
| requestedDeliveryDate | string | The requested delivery date for this line item, formatted as MM/DD/YYYY |
| confirmedDeliveryDate | string | The confirmed delivery date assigned by the system, formatted as MM/DD/YYYY |
| plantCode | string | The code of the plant fulfilling this line item |
| plantName | string | The name of the fulfilling plant |
| customerOrderReference | string | The customer's own purchase order reference associated with this line item |

### Example Response

```json
[
  {
    "deliveryNumber": "DEL-001",
    "lineItemNumber": "000010",
    "materialNumber": "PART-00451-A",
    "materialDescription": "Brake Pad Set Front Axle",
    "orderedQuantity": 4,
    "unitOfMeasure": "EA",
    "deliveryStatusCode": "A",
    "deliveryStatusText": "Open",
    "requestedDeliveryDate": "06/20/2026",
    "confirmedDeliveryDate": "06/22/2026",
    "plantCode": "P001",
    "plantName": "Central Warehouse",
    "customerOrderReference": "PO-2026-00891"
  },
  {
    "deliveryNumber": "DEL-002",
    "lineItemNumber": "000010",
    "materialNumber": "PART-00782-B",
    "materialDescription": "Oil Filter",
    "orderedQuantity": 10,
    "unitOfMeasure": "EA",
    "deliveryStatusCode": "C",
    "deliveryStatusText": "Completed",
    "requestedDeliveryDate": "07/07/2026",
    "confirmedDeliveryDate": "07/07/2026",
    "plantCode": "P001",
    "plantName": "Central Warehouse",
    "customerOrderReference": "PO-2026-00874"
  }
]
```

## Error Responses

If the session is invalid, an authentication error is returned. If the delivery numbers list is empty or missing, a validation error is returned. All error responses include a message field.

## Usage Notes for AI Agent

Call this endpoint after `getDeliveryList` when the user wants to see the individual parts within a specific delivery. Pass the `deliveryNumber` values obtained from the main list response. Multiple delivery numbers can be passed in a single call. The `lineItemNumber` from this response is required as input when calling the handling unit information endpoint for packaging details.
