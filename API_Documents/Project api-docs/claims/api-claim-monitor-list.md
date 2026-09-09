# API Documentation: Get Claim Monitor List

## Overview

This API retrieves a list of claims submitted by a dealer or service partner within a specified date range. It is the primary endpoint used to display the claim tracking overview screen. Each entry in the response represents one claim header with its current processing status, claim type, and rejection reason if applicable. This is the most important claims API for an AI agent as it directly answers questions about claim status.

## Endpoint

```
POST /api/claimtracking/getClaimMonitorList
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
| claimStatus | string | no | Filter by claim status code. Leave empty to retrieve all statuses |
| claimReason | string | no | Filter by claim reason or complaint type code. Leave empty for all |
| orderType | string | no | Filter by order type associated with the claim. Leave empty for all |
| rejectionReason | string | no | Filter by rejection reason code. Leave empty for all |
| dateFrom | string | yes | Start date of the search range in YYYYMMDD format |
| dateTo | string | yes | End date of the search range in YYYYMMDD format |
| maxResults | number | no | Maximum number of records to return. Leave empty for default limit |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/claimtracking/getClaimMonitorList" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "customerNumber": "10045",
    "claimStatus": "",
    "claimReason": "",
    "orderType": "",
    "rejectionReason": "",
    "dateFrom": "20260101",
    "dateTo": "20260630"
  }'
```

## Response

The response is a JSON array where each element represents one claim. The most relevant fields for an AI agent are the claim number, claim type, status, and rejection reason.

| Field | Type | Description |
|---|---|---|
| claimNumber | string | The unique internal claim reference number |
| claimReason | string | The claim reason or complaint type code |
| claimReasonText | string | Human readable description of the claim reason or type |
| orderType | string | The order type code associated with this claim |
| orderTypeText | string | Human readable description of the order type |
| rejectionReason | string | The rejection reason code if the claim was rejected. Empty if not rejected |
| rejectionReasonText | string | Human readable description of the rejection reason. Empty if not rejected |
| claimStatus | string | The current processing status code of the claim |
| claimStatusText | string | Human readable description of the current claim status |
| customerPurchaseOrderNumber | string | The customer purchase order number associated with the original order |
| orderNote | string | Any notes or comments associated with the claim |

### Claim Status Values

| Status | Meaning |
|---|---|
| Open | The claim has been submitted and is awaiting review |
| In Review | The claim is currently being assessed by the claims team |
| Awaiting Info | The claims team has requested additional information from the dealer |
| Approved | The claim has been accepted and a credit note will be issued |
| Rejected | The claim has been declined. The rejection reason field explains why |
| Credit Issued | The credit note has been generated and applied to the account |
| Cancelled | The claim was cancelled before processing was completed |

### Example Response

```json
[
  {
    "claimNumber": "CLM-2026-00234",
    "claimReason": "WR01",
    "claimReasonText": "Warranty Claim",
    "orderType": "ZRET",
    "orderTypeText": "Return Order",
    "rejectionReason": "",
    "rejectionReasonText": "",
    "claimStatus": "A",
    "claimStatusText": "In Review",
    "customerPurchaseOrderNumber": "PO-2026-00712",
    "orderNote": "Part failed within warranty period"
  },
  {
    "claimNumber": "CLM-2026-00198",
    "claimReason": "DM02",
    "claimReasonText": "Damage Claim",
    "orderType": "ZRET",
    "orderTypeText": "Return Order",
    "rejectionReason": "",
    "rejectionReasonText": "",
    "claimStatus": "C",
    "claimStatusText": "Credit Issued",
    "customerPurchaseOrderNumber": "PO-2026-00654",
    "orderNote": "Parts arrived damaged"
  },
  {
    "claimNumber": "CLM-2026-00156",
    "claimReason": "WR01",
    "claimReasonText": "Warranty Claim",
    "orderType": "ZRET",
    "orderTypeText": "Return Order",
    "rejectionReason": "EX01",
    "rejectionReasonText": "Part outside warranty period",
    "claimStatus": "R",
    "claimStatusText": "Rejected",
    "customerPurchaseOrderNumber": "PO-2026-00589",
    "orderNote": ""
  }
]
```

## Usage Notes for AI Agent

This endpoint should be called when a user asks about the status of their claims, for example "what is the status of my claim" or "why was my claim rejected". The claimStatusText field directly answers status questions. When a claim has a status of Rejected, the rejectionReasonText field should be included in the response to the user as it explains why the claim was declined. If the user asks about a specific claim by their purchase order number, the customerPurchaseOrderNumber field can be used to identify the relevant claim in the response.
