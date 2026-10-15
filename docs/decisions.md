# Design decisions

Each entry: what was decided, why, and what it costs.

## Structure

**Feature folders in one API project, not separate Domain/Application/Infrastructure projects.**
Layered folders (`Controllers`, `Services`, `Models`, `Data`) with business logic behind interfaces, and adapters for
outside systems in `Infrastructure/`. For an app this size, extra projects add references and mapping without making
anything work better. *Cost:* the boundaries are conventions, not compiler-enforced. If a second client or a larger domain
appeared, I'd split into projects.

**A variant is the sellable unit.** Every product has at least one variant ("Regular" for single-price items), and the
price lives on the variant. One code path for the cart, orders, and admin. *Cost:* single-price items carry a variant row.
Add-ons and modifiers were left out on purpose; they are a separate concept with their own rules.

## Money and orders

**The server decides every price.** The client sends variant ids and quantities. The API loads current prices, validates
availability, computes the total, and snapshots name and price onto each order item. *Why:* a tampered request can't change
what's charged, and history survives menu edits.

**Pesos stored as `numeric(10,2)`; centavos only at the payment boundary.** Conversion uses decimal arithmetic with
away-from-zero rounding (covered by a test for the classic `19.99 * 100` floating-point trap).

**Status changes follow an enforced state machine, with conditional updates.** The allowed moves live in one place and are
unit-tested for every pair. The update only applies if the status is still what the barista saw, so concurrent clicks
produce exactly one winner and a clear message for the other. *Cost:* the loser must refresh.

**Items with past orders are archived, not deleted.** Deleting returns a 409 that explains why; options that appear in past
orders can only be marked unavailable.

**Admins can't place orders.** Enforced by the API (403), not just hidden in the UI. Keeps staff accounts out of customer
data and order history.

## Payments

**PayMongo hosted checkout; the server trusts only signed webhooks.** The customer is redirected to PayMongo's page, so card
and wallet details never touch our servers. The "paid" state comes only from a webhook whose HMAC signature verifies, never
from the browser's return trip.

**The webhook is idempotent and strict.** One conditional update marks an order paid, so duplicate deliveries change nothing.
The event must match the order's own checkout session id and payment method. Unknown, malformed, or mismatched events are
acknowledged (so the provider stops retrying) and ignored. A payment that arrives after cancellation is recorded and logged
as needing a refund.

**Create the checkout session before saving the order.** If the gateway is down, nothing is persisted and the customer gets a
clear error. *Cost:* in the rare case the database write fails after the session exists, an unused session is left at the
provider, which is harmless.

**Pay at the counter is allowed, with guard rails.** Only for orders up to ₱1,000; unpaid online orders expire after 30
minutes; counter orders are cancelled 20 minutes after their pickup time if nobody collects them. A background job applies
these rules, and all thresholds are configuration.

## Email and order recovery

**A transactional outbox for email.** The email is saved in the same database transaction as the order, payment, or status
change that caused it, and a background worker sends it afterwards. *Why:* a provider outage, a timeout, or a sleeping
server can neither lose an email nor block an order, and you never get an email for a change that rolled back. *Cost:*
delivery is at-least-once, so in a rare crash between "sent" and "saved as sent" one email could repeat, and email is
delayed by up to the worker's 5-second tick.

**Retry only what can recover.** Timeouts, rate limits, and provider outages retry with growing delays (1 min, 5 min, 30 min,
2 h, 6 h). A rejected key, an unverified sender, or an invalid address fails immediately, so a configuration mistake
doesn't create a retry storm. Failed rows keep the provider's error for debugging, and finished rows are deleted after 30
days because they hold personal data.

**Brevo's HTTP API instead of SMTP.** Free hosting often blocks outbound SMTP, while an HTTPS call works anywhere. With no
API key configured, a log-only sender is used, so local development and tests need no email setup. Customer-supplied text
is HTML-escaped in every email.

**"Find my order" is two factors with an identical answer for every miss.** The order code plus the checkout email must
both match, only guest orders can be found (account orders stay private to their owner), and a wrong code, a wrong email,
a nonexistent order, and an account order all return the same response. Attempts are limited per visitor and per target
email, which stops someone from hammering codes against one person's address from many places. *Cost:* the message can't tell
a customer which of the two they got wrong.

## Authentication and security

**A backend-for-frontend holds the session.** The API issues a short-lived JWT; the Next.js server stores it in an `httpOnly`,
`Secure`, `SameSite=Lax` cookie and attaches it to requests, so page scripts never see it. State-changing handlers also
check the `Origin` header. *Cost:* logout is stateless (the token lives until it expires), and there are no refresh tokens.

**An allowlisted proxy.** The Next.js proxy forwards only the `orders` and `admin` areas. Path segments like `..` are
rejected, and the API still enforces roles on every request, so the proxy adds no privileges.

**Rate limiting per visitor behind two proxies.** Behind Vercel and Render, the API would see the same address for everyone.
The Next.js server sends the visitor's address with a shared secret, and the API only believes it when the secret matches.
Anyone calling the API directly is limited by the address it actually sees.

**Server-side validation of uploads.** Images are checked by their first bytes (not their names) and by size; the browser
also shrinks large photos before sending. Delivery sizes come from the Cloudinary URL.

## Platform

**Errors use RFC 7807 problem details everywhere** (`application/problem+json`), with field-level validation errors keyed
in camelCase so forms can show them directly.

**Polling, not push.** The order board refreshes every 10 seconds (it pauses when the tab is hidden) and the customer's page
polls every few seconds while a payment is pending. Simple and reliable on free hosting. SignalR is the natural upgrade.

**The cart lives in the browser.** It survives refreshes, and a server check on the cart and checkout pages flags removed
items or changed prices; the order itself is re-validated on the server. *Cost:* the cart doesn't follow you across devices.

**Pickup times are Philippine time.** The Philippines has no daylight saving, so a fixed +08:00 offset is correct and avoids a
dependency on time zone data in containers. Times are stored in UTC and always displayed with an explicit zone.

**Migrations run at start-up, behind a flag.** Fine for one small instance. With several instances, migrations would be a
separate deployment step.

## Testing

**Integration tests use real PostgreSQL (Testcontainers), not an in-memory database.** The behavior we rely on (conditional
updates, case-insensitive search, unique indexes, transactions) differs in fake databases. Only true external boundaries are
faked: the clock, the payment network, image storage, and the email provider. The real payment gateway and email worker
code run against those fakes.

**End-to-end tests run an isolated stack** on their own ports and database, and start fresh servers every run, because
reusing a stale server once produced a confusing failure. Test data is unique per test, so tests don't depend on each other.

**The tests found real bugs.** The integration suite caught a regression where the order-status endpoint had stopped
enforcing its rules after a refactor, something neither the unit tests nor manual checks had noticed.