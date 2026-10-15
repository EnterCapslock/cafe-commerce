# Architecture

## System overview

See the diagram in the [README](../README.md#architecture). In short: public reads (the menu and product options) go from
the browser straight to the API. Everything that needs a session (sign-in, ordering, the admin area) goes through the
Next.js server, which holds the JWT in an `httpOnly` cookie and forwards requests to the API with it. The API owns all
business rules and talks to PostgreSQL, PayMongo, Cloudinary, and Brevo.

## Placing and paying for an online order

```mermaid
sequenceDiagram
    actor C as Customer
    participant W as Next.js (Vercel)
    participant A as .NET API (Render)
    participant P as PayMongo

    C->>W: Check out, choose "Pay online"
    W->>A: POST /api/orders (token from the cookie, if signed in)
    A->>A: Validate, price from the database, snapshot items
    A->>P: Create checkout session (amount in centavos)
    P-->>A: Session id and checkout URL
    A-->>W: Order (Unpaid) with the checkout URL
    W-->>C: Redirect to PayMongo
    C->>P: Pay (GCash, Maya, card, ...)
    P->>A: Webhook: checkout_session.payment.paid (signed)
    A->>A: Verify the signature, mark the order Paid (idempotent)
    C->>W: Return to /orders/{id}
    W->>A: Poll the order
    A-->>W: Paid
```

If PayMongo is unreachable when the order is placed, nothing is saved and the customer is told to try again (or pay at the
counter, when the order qualifies). A background job expires unpaid online orders after 30 minutes.

## Order emails: the transactional outbox

```mermaid
sequenceDiagram
    participant A as API request
    participant D as PostgreSQL
    participant W as Email worker (background)
    participant B as Brevo

    A->>D: BEGIN
    A->>D: Save the order, payment, or status change
    A->>D: Save the email in the EmailOutbox table
    A->>D: COMMIT (both, or neither)
    loop every 5 seconds
        W->>D: Pick emails that are due
        W->>B: Send (HTTP API)
        alt sent
            W->>D: Mark Sent
        else temporary failure (timeout, 429, 5xx)
            W->>D: Retry later: 1 min, 5 min, 30 min, 2 h, 6 h
        else permanent failure (bad key, unverified sender, invalid address)
            W->>D: Mark Failed
        end
    end
```

Emails: *order received* (counter orders when placed, online orders when the payment is confirmed) and *ready for pickup*.
An order paid after it was cancelled is not confirmed by email, because it needs a refund instead. Sent and failed rows are
deleted after 30 days.

## Finding a guest order

A guest who lost the link enters the 8-character order code (the start of the order id) and the email used at checkout.
Only guest orders can be found this way, and every kind of miss returns the same answer, so the endpoint never reveals
which orders exist. Attempts are limited per visitor (10 a minute) and per target email (10 an hour).

## Order status

```mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> Preparing
    Preparing --> ReadyForPickup
    ReadyForPickup --> Completed
    Pending --> Cancelled
    Preparing --> Cancelled
    ReadyForPickup --> Cancelled
    Completed --> [*]
    Cancelled --> [*]
```

Only these moves are allowed, and the server enforces them. Online orders can't advance until they're paid. Completing a
pay-at-counter order marks it paid, because the money was collected then.

## Database schema

```mermaid
erDiagram
    USERS |o--o{ ORDERS : "places (guest orders have no user)"
    PRODUCTS ||--|{ PRODUCT_VARIANTS : offers
    PRODUCT_VARIANTS ||--o{ ORDER_ITEMS : "ordered as"
    ORDERS ||--|{ ORDER_ITEMS : contains
    ORDERS ||..o{ EMAIL_OUTBOX : "emails about"

    USERS {
        int Id PK
        string Name
        string Email UK
        string PasswordHash
        string Role
    }
    PRODUCTS {
        int Id PK
        string Name
        string Category
        string ImageUrl
        bool IsActive
    }
    PRODUCT_VARIANTS {
        int Id PK
        int ProductId FK
        string Name
        decimal Price
        bool IsAvailable
        int SortOrder
    }
    ORDERS {
        uuid Id PK
        int UserId FK
        string CustomerName
        datetime PickupTime
        decimal TotalAmount
        string Status
        string PaymentMethod
        string PaymentStatus
        string PaymentReference
    }
    ORDER_ITEMS {
        int Id PK
        uuid OrderId FK
        int ProductVariantId FK
        string ProductName
        string VariantName
        int Quantity
        decimal PriceAtPurchase
    }
    EMAIL_OUTBOX {
        uuid Id PK
        uuid OrderId
        string Kind
        string ToAddress
        string Subject
        string Status
        int Attempts
        datetime NextAttemptAt
    }
```

Notes: money is `numeric(10,2)` in pesos; enums are stored as strings; timestamps are UTC. Order items copy the product and
option names and the price at purchase, so history stays correct after the menu changes. Items with past orders can't be
deleted, only archived. The outbox deliberately has no foreign key to orders, so its rows can outlive an order.