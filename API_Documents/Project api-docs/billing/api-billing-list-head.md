# API Documentation: Billing List Head

## Overview

This API retrieves a list of billing document headers for a customer within a specified date range. Each entry represents one billing document at the header level, showing its type, date, total value, and currency. This is the primary entry point for the Billing List module and is used to display the billing overview screen.

## Endpoint

```
POST /api/billinglist/getBillingList
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

The request body is a JSON object. The main filter criteria are provided in a header object along with optional lists for narrowing results by purchase order number, material number, or billing document number.

| Field | Type | Required | Description |
|---|---|---|---|
| customerNumber | string | yes | The ship-to customer number used to fetch billing documents for that account |
| billingDocumentType | string | no | Filter by billing document type code. Leave empty to retrieve all types |
| dateFrom | timestamp | yes | Start of the billing date range in Unix epoch milliseconds |
| dateTo | timestamp | yes | End of the billing date range in Unix epoch milliseconds |
| maxResults | integer | no | Maximum number of records to return. Typical value is 1000 |
| customerOrderNumbers | array | no | List of customer purchase order number strings to filter by. Pass empty array for no filter |
| materialNumbers | array | no | List of material or part number strings to filter by. Pass empty array for no filter |
| billingDocumentNumbers | array | no | List of specific billing document number strings to filter by. Pass empty array for no filter |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/billinglist/getBillingList" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "customerNumber": "10045",
    "billingDocumentType": "",
    "dateFrom": 1780000000000,
    "dateTo": 1785000000000,
    "maxResults": 1000,
    "customerOrderNumbers": [],
    "materialNumbers": [],
    "billingDocumentNumbers": []
  }'
```

## Response

The response is a JSON array where each element represents one billing document header.

| Field | Type | Description |
|---|---|---|
| customerNumber | string | The ship-to customer number associated with the billing document |
| billingDocumentNumber | string | The unique billing document number assigned by the system |
| billingDocumentType | string | The type code of the billing document, for example invoice or credit memo |
| billingDocumentTypeText | string | Human readable description of the billing document type |
| billingDate | string | The date the billing document was created, formatted as MM/DD/YYYY |
| netValue | number | The net value of the billing document before tax |
| taxValue | number | The tax amount applied to the billing document |
| totalValue | number | The total value of the billing document including tax |
| currency | string | The currency code for all monetary values in this document |
| customerOrderReference | string | The customer's own purchase order reference associated with this billing document |

### Example Response

```json
[
  {
    "customerNumber": "10045",
    "billingDocumentNumber": "BILL-001",
    "billingDocumentType": "INV",
    "billingDocumentTypeText": "Invoice",
    "billingDate": "06/20/2026",
    "netValue": 1205.60,
    "taxValue": 192.90,
    "totalValue": 1398.50,
    "currency": "EUR",
    "customerOrderReference": "PO-2026-00874"
  },
  {
    "customerNumber": "10045",
    "billingDocumentNumber": "BILL-002",
    "billingDocumentType": "CRD",
    "billingDocumentTypeText": "Credit Memo",
    "billingDate": "06/25/2026",
    "netValue": -342.80,
    "taxValue": -54.85,
    "totalValue": -397.65,
    "currency": "EUR",
    "customerOrderReference": "PO-2026-00891"
  }
]
```

## Error Responses

If the session has expired or is invalid, the server returns an authentication error. If required fields such as the customer number or date range are missing, the server returns a validation error. If the backend system is temporarily unavailable, a service unavailable error is returned. All error responses include a message field describing the reason for the failure.

## Usage Notes for AI Agent

Call this endpoint when the user asks about invoices, billing documents, credit memos, or wants to review their billing history. Use the `billingDocumentNumber` values from the response to make follow-up calls to the billing item endpoint for line-item details. When the user asks about a specific billing document by their own purchase order reference, pass it in the `customerOrderNumbers` array. When the user asks about billing for a specific part, pass it in the `materialNumbers` array. Credit memos will appear as negative values in the `netValue` and `totalValue` fields.
