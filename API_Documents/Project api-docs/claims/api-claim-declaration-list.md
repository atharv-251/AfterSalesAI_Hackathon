# API Documentation: Get Claim Declaration List

## Overview

This API retrieves the eligible billing documents and their line items that can be used as the basis for a new claim declaration. It is called when a dealer wants to raise a claim against a specific invoice or delivery, and the system needs to look up what was purchased so the dealer can select which parts to claim against. Each item in the response represents a purchasable line from a billing document, including the quantities purchased, quantities already claimed, and the remaining claimable quantity.

## Endpoint

```
POST /api/claimdeclaration/getClaimDeclarationList
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

| Field | Type | Required | Description |
|---|---|---|---|
| customerNumber | string | yes | The dealer or service partner customer number |
| invoiceNumbers | array of strings | no | List of specific invoice or billing document numbers to look up. Pass empty array to retrieve all eligible documents |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/claimdeclaration/getClaimDeclarationList" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "customerNumber": "10045",
    "invoiceNumbers": ["INV-2026-00445"]
  }'
```

## Response

The response is a JSON object with a success flag and a data array. Each item in the data array represents one claimable line from a billing document.

| Field | Type | Description |
|---|---|---|
| success | boolean | Indicates whether the lookup was successful |
| data | array | List of claimable billing line items |

Each item in the data array contains:

| Field | Type | Description |
|---|---|---|
| invoiceNumber | string | The billing document or invoice number |
| lineItemNumber | string | The line item position within the invoice |
| partNumber | string | The part or material number |
| partDescription | string | The human readable description of the part |
| referenceOrderNumber | string | The original sales order number that this invoice line relates to |
| referenceLineItemNumber | string | The line item number in the original sales order |
| invoicedQuantity | number | The quantity that was invoiced on this billing document |
| originalOrderQuantity | number | The quantity from the original sales order |
| remainingClaimableQuantity | number | The quantity still available to be claimed. This is the invoiced quantity minus any quantities already claimed |
| unitOfMeasure | string | The unit of measure for the quantities |
| listPrice | number | The standard list price per unit |
| netPrice | number | The dealer net price per unit after discounts |
| currency | string | The currency code for all monetary values |
| customerNumber | string | The customer number associated with this billing document |

### Example Response

```json
{
  "success": true,
  "data": [
    {
      "invoiceNumber": "INV-2026-00445",
      "lineItemNumber": "10",
      "partNumber": "PART-00451-A",
      "partDescription": "Bracket Assembly Front Axle",
      "referenceOrderNumber": "4000123456",
      "referenceLineItemNumber": "10",
      "invoicedQuantity": 2.000,
      "originalOrderQuantity": 2.000,
      "remainingClaimableQuantity": 2.000,
      "unitOfMeasure": "PC",
      "listPrice": 125.50,
      "netPrice": 100.44,
      "currency": "EUR",
      "customerNumber": "10045"
    },
    {
      "invoiceNumber": "INV-2026-00445",
      "lineItemNumber": "20",
      "partNumber": "PART-00678-D",
      "partDescription": "Air Mass Flow Sensor",
      "referenceOrderNumber": "4000123456",
      "referenceLineItemNumber": "40",
      "invoicedQuantity": 1.000,
      "originalOrderQuantity": 1.000,
      "remainingClaimableQuantity": 0.000,
      "unitOfMeasure": "PC",
      "listPrice": 232.75,
      "netPrice": 186.20,
      "currency": "EUR",
      "customerNumber": "10045"
    }
  ]
}
```

### Example Error Response

```json
{
  "success": false,
  "data": "Invoice INV-2026-00445 could not be found or does not belong to the specified customer account."
}
```

## Usage Notes for AI Agent

This endpoint should be called when a user wants to raise a claim and needs to see which parts from a specific invoice are eligible. The remainingClaimableQuantity field is critical — if it is zero, the part has already been fully claimed and cannot be claimed again. The agent should highlight this to the user if they are trying to claim a part that has no remaining claimable quantity. The referenceOrderNumber field links the invoice line back to the original order, which is useful when the user refers to an order number rather than an invoice number. When the success field is false, the data field contains an error message string rather than an array, and this message should be relayed to the user.
