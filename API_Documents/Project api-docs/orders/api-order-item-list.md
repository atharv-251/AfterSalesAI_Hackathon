# API Documentation: Get Order Item List

## Overview

This API retrieves the individual line items belonging to one or more orders. It is called after the order list API to drill down into the details of a specific order. Each entry in the response represents one order line item with its part number, description, quantity, net value, and current fulfilment status. Some orders may contain sub-items linked to a parent line item, which is indicated by the parent line item number field.

## Endpoint

```
POST /api/orderTracking/getOrderItemList
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

The request body is a JSON object containing a list of order numbers for which the line items should be retrieved. Multiple order numbers can be passed in a single request.

| Field | Type | Required | Description |
|---|---|---|---|
| orderNumbers | array of strings | yes | List of one or more internal order numbers for which line items should be fetched |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/orderTracking/getOrderItemList" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "orderNumbers": ["4000123456"]
  }'
```

## Response

The response is a JSON array where each element represents one order line item. Items with a non-zero parent line item number are sub-items linked to the parent line.

| Field | Type | Description |
|---|---|---|
| orderNumber | string | The internal order number this line item belongs to |
| lineItemNumber | string | The sequential position number of this line item within the order |
| parentLineItemNumber | string | If this is a sub-item, this field contains the line item number of the parent. Zero means it is a top-level item |
| partNumber | string | The unique part or material number |
| partDescription | string | The human readable description of the part |
| orderedQuantity | number | The quantity that was originally ordered |
| confirmedQuantity | number | The quantity confirmed for delivery by the warehouse |
| deliveredQuantity | number | The quantity that has already been delivered |
| unitOfMeasure | string | The unit in which the part is sold, for example PC for piece or PAC for pack |
| netValue | number | The net value of this line item |
| depositValue | number | Any deposit or surcharge value applied to this line item |
| discountValue | number | Any discount applied to this line item |
| warehouse | string | The warehouse or plant code from which this item will be fulfilled |
| productHierarchy | string | Internal product hierarchy classification code for the part |
| deliveryReadyFlag | string | Indicates whether this item is ready for delivery. X means ready, empty means not ready |
| rejectionReason | string | If the line item was rejected, this field contains the rejection reason code. Empty if not rejected |
| requestedDeliveryDate | string | The requested delivery date for this line item. Value of 00000000 means no specific date was set |
| lineItemStatus | string | Single character code representing the fulfilment status of this line item |
| lineItemStatusText | string | Human readable description of the line item status |

### Line Item Status Codes

| Status Code | Status Text | Meaning |
|---|---|---|
| A | Open | The line item has been received and is awaiting fulfilment |
| B | In Progress | The item is being picked or processed at the warehouse |
| C | Completed | The item has been fully delivered |
| (empty) | Not relevant | This line item is not relevant for delivery tracking, typically a parent grouping item |

### Example Response

```json
[
  {
    "orderNumber": "4000123456",
    "lineItemNumber": "10",
    "parentLineItemNumber": "0",
    "partNumber": "PART-00451-A",
    "partDescription": "Bracket Assembly Front Axle",
    "orderedQuantity": 2.000,
    "confirmedQuantity": 2.000,
    "deliveredQuantity": 0.000,
    "unitOfMeasure": "PC",
    "netValue": 200.88,
    "depositValue": 0.00,
    "discountValue": 0.00,
    "warehouse": "WH01",
    "productHierarchy": "E540C",
    "deliveryReadyFlag": "X",
    "rejectionReason": "",
    "requestedDeliveryDate": "00000000",
    "lineItemStatus": "A",
    "lineItemStatusText": "Open"
  },
  {
    "orderNumber": "4000123456",
    "lineItemNumber": "20",
    "parentLineItemNumber": "0",
    "partNumber": "PART-00892-B",
    "partDescription": "Hex Flange Nut M12",
    "orderedQuantity": 1.000,
    "confirmedQuantity": 0.000,
    "deliveredQuantity": 0.000,
    "unitOfMeasure": "PC",
    "netValue": 0.00,
    "depositValue": 0.00,
    "discountValue": 0.00,
    "warehouse": "WH01",
    "productHierarchy": "F390D",
    "deliveryReadyFlag": "",
    "rejectionReason": "",
    "requestedDeliveryDate": "00000000",
    "lineItemStatus": "",
    "lineItemStatusText": "Not relevant"
  },
  {
    "orderNumber": "4000123456",
    "lineItemNumber": "21",
    "parentLineItemNumber": "20",
    "partNumber": "PART-00892-B",
    "partDescription": "Hex Flange Nut M12",
    "orderedQuantity": 1.000,
    "confirmedQuantity": 1.000,
    "deliveredQuantity": 0.000,
    "unitOfMeasure": "PC",
    "netValue": 8.05,
    "depositValue": 0.00,
    "discountValue": 0.00,
    "warehouse": "WH01",
    "productHierarchy": "F390D",
    "deliveryReadyFlag": "X",
    "rejectionReason": "",
    "requestedDeliveryDate": "00000000",
    "lineItemStatus": "A",
    "lineItemStatusText": "Open"
  },
  {
    "orderNumber": "4000123456",
    "lineItemNumber": "30",
    "parentLineItemNumber": "0",
    "partNumber": "PART-01103-C",
    "partDescription": "Intermediate Shim Plate",
    "orderedQuantity": 1.000,
    "confirmedQuantity": 1.000,
    "deliveredQuantity": 0.000,
    "unitOfMeasure": "PC",
    "netValue": 51.84,
    "depositValue": 0.00,
    "discountValue": 0.00,
    "warehouse": "WH01",
    "productHierarchy": "FD40B",
    "deliveryReadyFlag": "X",
    "rejectionReason": "",
    "requestedDeliveryDate": "00000000",
    "lineItemStatus": "A",
    "lineItemStatusText": "Open"
  },
  {
    "orderNumber": "4000123456",
    "lineItemNumber": "40",
    "parentLineItemNumber": "0",
    "partNumber": "PART-00678-D",
    "partDescription": "Air Mass Flow Sensor",
    "orderedQuantity": 1.000,
    "confirmedQuantity": 1.000,
    "deliveredQuantity": 0.000,
    "unitOfMeasure": "PC",
    "netValue": 186.20,
    "depositValue": 0.00,
    "discountValue": 0.00,
    "warehouse": "WH01",
    "productHierarchy": "3020A",
    "deliveryReadyFlag": "X",
    "rejectionReason": "",
    "requestedDeliveryDate": "00000000",
    "lineItemStatus": "A",
    "lineItemStatusText": "Open"
  },
  {
    "orderNumber": "4000123456",
    "lineItemNumber": "50",
    "parentLineItemNumber": "0",
    "partNumber": "PART-00234-E",
    "partDescription": "Sealing Kit Single Wire",
    "orderedQuantity": 10.000,
    "confirmedQuantity": 10.000,
    "deliveredQuantity": 0.000,
    "unitOfMeasure": "PAC",
    "netValue": 22.32,
    "depositValue": 0.00,
    "discountValue": 0.00,
    "warehouse": "WH01",
    "productHierarchy": "4080C",
    "deliveryReadyFlag": "X",
    "rejectionReason": "",
    "requestedDeliveryDate": "00000000",
    "lineItemStatus": "A",
    "lineItemStatusText": "Open"
  }
]
```

## Error Responses

If no order numbers are provided in the request, the server returns a validation error. If an order number does not exist or does not belong to the authenticated user's account, the response will be an empty array for that order. General session and service availability errors follow the same pattern as described in the order list API documentation.

## Usage Notes for AI Agent

This endpoint should be called when a user asks about the specific parts within an order, for example "what parts are in my order" or "has part X been delivered yet". The order number from the order list API response should be passed here. When interpreting the response, items with a status of Not relevant are parent grouping items and should generally not be shown to the user as individual deliverable items. The actual deliverable item is the corresponding sub-item with a non-zero parent line item number. The delivery ready flag being set to X means the warehouse has confirmed the item is ready to ship. If the net value of a line item is zero and the status is Not relevant, it is a structural grouping line and carries no independent value.
