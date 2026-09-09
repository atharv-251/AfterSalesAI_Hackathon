# API Documentation: Billing List Detail

## Overview

This API retrieves detailed reference information for a specific line item within a billing document. The detail level provides the deepest view into a billing entry, including pricing breakdown, billed and original delivery quantities, and cross-references to the originating delivery and order documents. It is used when the user drills down from a billing line item to inspect its full detail.

## Endpoint

```
POST /api/billinglist/getBillingDetail
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
| billingDocumentNumber | string | yes | The billing document number, obtained from the billing list head response |
| lineItemNumber | string | yes | The line item position number within the billing document, obtained from the billing list item response |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/billinglist/getBillingDetail" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "billingDocumentNumber": "BILL-001",
    "lineItemNumber": "000010"
  }'
```

## Response

The response contains the full detail for the specified billing line item, including pricing information, quantities, unit of measure, currency, and cross-references to the originating delivery document and delivery line item.

| Field | Type | Description |
|---|---|---|
| billingDocumentNumber | string | The billing document number |
| lineItemNumber | string | The line item position number within the billing document |
| materialNumber | string | The part or material number |
| materialDescription | string | The description of the part |
| billedQuantity | number | The quantity included in this billing document |
| originalDeliveryQuantity | number | The total quantity from the originating delivery |
| openQuantity | number | The quantity not yet fully settled or credited |
| unitOfMeasure | string | The unit of measure for all quantities |
| listPrice | number | The list price per unit before discounts |
| netPrice | number | The net price per unit after discounts |
| currency | string | The currency code for all monetary values |
| referenceDeliveryNumber | string | The delivery document number that this billing line item references |
| referenceDeliveryLineItem | string | The line item number in the referenced delivery document |

### Example Response

```json
{
  "billingDocumentNumber": "BILL-001",
  "lineItemNumber": "000010",
  "materialNumber": "PART-00782-B",
  "materialDescription": "Oil Filter",
  "billedQuantity": 10,
  "originalDeliveryQuantity": 10,
  "openQuantity": 0,
  "unitOfMeasure": "EA",
  "listPrice": 132.50,
  "netPrice": 120.56,
  "currency": "EUR",
  "referenceDeliveryNumber": "DEL-002",
  "referenceDeliveryLineItem": "000010"
}
```

## Error Responses

If the session is invalid, an authentication error is returned. If either the billing document number or line item number is missing, a validation error is returned. All error responses include a message field.

## Usage Notes for AI Agent

Call this endpoint only when the user explicitly asks for the full detail of a specific billing line item, such as pricing breakdown, quantity reconciliation, or the originating delivery reference. You must first obtain the `billingDocumentNumber` from `getBillingList` and the `lineItemNumber` from `getBillingItemList` before calling this endpoint. This is a drill-down endpoint and should not be called proactively. The `referenceDeliveryNumber` field is useful when the user wants to trace a billing entry back to the original delivery.
