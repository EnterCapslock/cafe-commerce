import { defineConfig, devices } from "@playwright/test";

// A separate stack on separate ports, so e2e never touches your dev servers or dev data.
const API_PORT = 5180;
const WEB_PORT = 3100;
const API_URL = `http://localhost:${API_PORT}`;
const WEB_URL = `http://localhost:${WEB_PORT}`;

// Match the host port in your docker-compose.yml (5433 if you remapped it, otherwise 5432).
// The database is created automatically on first run.
const DB =
  process.env.E2E_DB_CONNECTION ??
  "Host=localhost;Port=5433;Database=cafe_e2e;Username=postgres;Password=postgres";

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 2 : undefined,
  reporter: [[process.env.CI ? "github" : "list"], ["html", { open: "never" }]],

  use: {
    baseURL: WEB_URL,
    trace: "on-first-retry",
    screenshot: "only-on-failure",
  },

  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],

  webServer: [
    {
      // Release configuration builds into bin/Release, so it never clashes with a running dev API.
      command: "dotnet run --no-launch-profile --configuration Release",
      cwd: "../backend/src/Cafe.Api",
      url: `${API_URL}/health`,
      reuseExistingServer: process.env.E2E_REUSE === "1",
      timeout: 180_000,
      // stdout: "pipe", // uncomment to see the API's logs while debugging
      env: {
        ASPNETCORE_ENVIRONMENT: "Development", // enables auto-migrate and seeding
        ASPNETCORE_URLS: API_URL,
        ConnectionStrings__Default: DB,
        Cors__AllowedOrigins__0: WEB_URL,
        Jwt__Key: "e2e-only-signing-key-at-least-32-bytes-long!!",
        Jwt__Issuer: "cafe-api",
        Jwt__Audience: "cafe-web",
        Seed__AdminEmail: "admin@cafe.local",
        Seed__AdminPassword: "E2eAdmin#12345",
        RateLimits__AuthPerMinute: "100000",
        RateLimits__OrdersPerMinute: "100000",
        RateLimits__LookupPerMinute: "100000",
        // PayMongo points at a dead address: "Pay online" fails instantly and offline.
        PayMongo__SecretKey: "sk_test_e2e_not_a_real_key",
        PayMongo__BaseUrl: "http://127.0.0.1:9/",
        Frontend__BaseUrl: WEB_URL,
        Logging__LogLevel__Default: "Warning",
      },
    },
    {
      command: `npm run build && npx next start -p ${WEB_PORT}`,
      url: WEB_URL,
      reuseExistingServer: process.env.E2E_REUSE === "1",
      timeout: 300_000,
      env: {
        NEXT_PUBLIC_API_URL: API_URL, // baked into the build, and used by server-side calls
        API_URL,
      },
    },
  ],
});