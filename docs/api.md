# API reference

JSON over HTTPS. Errors use RFC 7807 problem details (`application/problem+json`). Interactive docs are available at
`/scalar/v1` in development. "Admin" endpoints require a token whose role is `Admin`.

## Public

| Method | Path | Notes |
|---|---|---|
| GET | `/api/products?category=` | Active menu items with a "from" price |
| GET | `/api/products/{id}` | One item with all options |
| POST | `/api/products/check` | Current availability and price for a list of variant ids |

## Accounts

| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `/api/auth/register` | none | Creates a customer. Rate limited |
| POST | `/api/auth/login` | none | Returns a JWT. Rate limited |
| GET | `/api/auth/me` | token | The current user's profile |

## Orders

| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `/api/orders` | optional | Guest or signed-in customer. Admins get 403. Returns 201, 400 (validation), 422 (unavailable items), or 502 (payment provider down) |
| GET | `/api/orders/{id}` | optional | Guest orders open by id. Account orders: owner and admins only (404 otherwise) |
| GET | `/api/orders/mine` | token | The customer's 50 most recent orders |
| POST | `/api/orders/lookup` | none | Finds a **guest** order from its 8-character code and the checkout email. Every miss returns the same 404. Limited per visitor and per email (429) |
| POST | `/api/payments/webhook` | signature | PayMongo events. Verified by HMAC signature, never by login |

## Admin

| Method | Path | Notes |
|---|---|---|
| GET | `/api/admin/orders?status=&search=&limit=` | The live board by default; history with `Completed` or `Cancelled` |
| PATCH | `/api/admin/orders/{id}/status` | Enforces the status rules. 409 for illegal or conflicting moves. Moving to `ReadyForPickup` queues the pickup email |
| GET / POST | `/api/admin/products` | All items (including archived); create with options |
| GET / PUT / DELETE | `/api/admin/products/{id}` | PUT syncs options by id. DELETE returns 409 if the item has orders |
| POST / DELETE | `/api/admin/products/{id}/image` | Multipart upload (JPEG, PNG, WebP up to 4 MB) and removal |

## Operations

| Path | Notes |
|---|---|
| `/health/live` | Process is up (used by the host's probe) |
| `/health` | Includes a database check; 503 if it fails |