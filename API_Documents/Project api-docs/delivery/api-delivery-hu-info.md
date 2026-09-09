# API Documentation: Delivery Handling Unit Information

## Overview

This API retrieves handling unit information for a specific line item within a delivery order. A handling unit represents the physical packaging unit — such as a pallet, box, or container — in which the ordered parts are shipped. The response provides tracking-level detail including the handling unit identifier, weight, and shipment reference. This endpoint is used when the user needs packaging or physical shipment tracking details for a specific delivery line item.

## Endpoint

```
POST /api/deliverystatus/getDeliveryHuInfo
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
| deliveryNumber | string | yes | The delivery document number, obtained from the delivery status main list |
| lineItemNumber | string | yes | The line item position number within the delivery, obtained from the delivery status item list |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/deliverystatus/getDeliveryHuInfo" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "deliveryNumber": "DEL-001",
    "lineItemNumber": "000010"
  }'
```

## Response

The response contains handling unit details for the specified delivery line item, including the handling unit identifier, gross weight, net weight, unit of weight, and any associated shipment or transport reference numbers.

## Key Behaviors

This endpoint requires both the delivery document number and the specific line item number. Both values must be obtained from prior calls to the delivery status endpoints. This endpoint is only meaningful for completed or partially shipped deliveries where handling units have been assigned. For open orders where goods have not yet been issued, the response may be empty.

## Error Responses

If the session is invalid, an authentication error is returned. If either the delivery number or line item number is missing, a validation error is returned. All error responses include a message field.

## Usage Notes for AI Agent

Call this endpoint only when the user explicitly asks for handling unit, packaging, or physical shipment tracking details for a specific delivery line item. You must first obtain the `deliveryNumber` from `getDeliveryList` and the `lineItemNumber` from `getDeliveryItemList` before calling this endpoint. Do not call this endpoint proactively — it is a drill-down endpoint triggered by specific user intent around packaging or tracking details.
