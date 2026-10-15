# Configuration

Secrets are never committed. Locally they live in .NET user-secrets and `frontend/.env.local`; in production they live in the
hosts' dashboards. In configuration, `:` and `__` are interchangeable (`Jwt:Key` = `Jwt__Key`).

## API (`backend/src/Cafe.Api`)

| Setting | Required | Purpose |
|---|---|---|
| `ConnectionStrings:Default` | yes | PostgreSQL connection string |
| `Jwt:Key` | yes | Signing key, at least 32 bytes (`openssl rand -base64 48`) |
| `Jwt:Issuer`, `Jwt:Audience` | yes | Token issuer and audience (`cafe-api`, `cafe-web`) |
| `Jwt:ExpiryMinutes` | no | Token lifetime (default 60) |
| `Cors:AllowedOrigins:0` | yes | The web app's origin, for example `https://your-app.vercel.app` |
| `Seed:AdminEmail`, `Seed:AdminPassword` | first run | Creates the admin account if it doesn't exist |
| `Database:MigrateOnStartup` | production | `true` applies migrations and seeds on start-up (always on in Development) |
| `Frontend:BaseUrl` | for payments and emails | The web app's address: where PayMongo returns customers and the base of links in emails (default `http://localhost:3000`) |
| `PayMongo:SecretKey` | for payments | A `sk_test_...` key. Never a live key |
| `PayMongo:WebhookSecret` | for payments | Secret of the webhook endpoint |
| `PayMongo:PaymentMethodTypes` | no | Defaults to `gcash`, `qrph`, `card` (set in `appsettings.json`) |
| `Cloudinary:CloudName`, `:ApiKey`, `:ApiSecret` | for photos | Without them, uploads return 503 and everything else works |
| `Email:BrevoApiKey` | for emails | Brevo API key. Without it, emails are written to the log instead of sent |
| `Email:FromAddress` | for emails | A sender address verified in Brevo |
| `Email:FromName` | no | Display name on emails (default `The Cafe`) |
| `Proxy:SharedSecret` | production | Must equal the web app's `PROXY_SHARED_SECRET`; enables per-visitor rate limits |
| `Orders:*` | no | `MinLeadMinutes` 15, `MaxAdvanceDays` 7, `MaxCounterPaymentAmount` 1000, `UnpaidExpiryMinutes` 30, `NoShowMinutes` 20 |
| `RateLimits:AuthPerMinute`, `:OrdersPerMinute`, `:LookupPerMinute` | no | Defaults 10, 20, and 10 per visitor |
| `PORT` | hosts | Port to listen on (Render sets it; the image defaults to 8080) |

## Web app (`frontend`)

| Variable | Purpose |
|---|---|
| `NEXT_PUBLIC_API_URL` | The API's address. Public, baked into the build: redeploy after changing it |
| `API_URL` | Server-side API address (defaults to `NEXT_PUBLIC_API_URL`) |
| `PROXY_SHARED_SECRET` | Server-only; must equal the API's `Proxy:SharedSecret` |

## Deploying

1. **Neon:** create a project and copy the **direct** connection (not the pooled one), then write it in Npgsql form:
   `Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=VerifyFull;Timeout=30;Connection Idle Lifetime=30;Connection Pruning Interval=10;Maximum Pool Size=10`
2. **Render:** a Docker web service. Dockerfile path `backend/src/Cafe.Api/Dockerfile`, build context
   `backend/src/Cafe.Api`, health check path `/health/live`. Set `PORT=8080`, `ASPNETCORE_ENVIRONMENT=Production`,
   `Database__MigrateOnStartup=true`, plus the API settings above.
3. **Vercel:** import the repository with **Root Directory** `frontend`, set the three web variables, and choose a function
   region near the API.
4. Set `Cors__AllowedOrigins__0` and `Frontend__BaseUrl` on Render to the Vercel URL.
5. In PayMongo's dashboard (test mode), add `https://<api>/api/payments/webhook` for `checkout_session.payment.paid` and
   store its secret as `PayMongo__WebhookSecret`.
6. **Brevo:** create an account, verify a sender address, create an API key, and set `Email__BrevoApiKey`,
   `Email__FromAddress`, and `Email__FromName`. The free plan has a daily sending cap and may add Brevo branding; a sender on
   your own domain with DKIM gives the best deliverability.

Health endpoints: `/health/live` (is the process up?) and `/health` (can it reach the database?).