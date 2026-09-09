# API Documentation: Save Favourite Plants

## Overview

This API saves a user's selected list of favourite plants for use in the Parts Locator module. Once saved, these plants are remembered for future sessions and can be quickly retrieved using the `getPlantList` endpoint with the favourites-only flag. This is a write operation that persists the user's plant preferences in the system.

## Endpoint

```
POST /api/partslocator/saveFavouritePlants
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
| plantCodes | array | yes | List of plant code strings to save as the user's favourites. The existing favourite list is replaced entirely by this new list |

### Example Request

```bash
curl -X POST "https://your-parts-portal.example.com/api/partslocator/saveFavouritePlants" \
  -H "Accept: application/json" \
  -H "Content-Type: application/json" \
  -H "X-Requested-With: XMLHttpRequest" \
  -b "JSESSIONID=<your-session-id>" \
  --data-raw '{
    "plantCodes": ["WH01", "WH03"]
  }'
```

## Response

The response confirms whether the save operation was successful.

## Key Behaviors

This endpoint replaces the user's entire existing favourite plant list with the new list provided. To remove all favourites, pass an empty array. Plant codes must be valid codes obtainable from the `userPlantList` endpoint.

## Error Responses

If the session is invalid, an authentication error is returned. If any plant code in the list is invalid, a validation error is returned. All error responses include a message field.

## Usage Notes for AI Agent

Call this endpoint only when the user explicitly asks to save or update their favourite plants. This is a write operation that modifies user preferences, so it should only be called on explicit user instruction. Always obtain valid plant codes from `getPlantList` before calling this endpoint.
