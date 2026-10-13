import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";
import { API_URL, WEB_URL, apiRegister, appReady, PASSWORD, signIn, uniqueEmail } from "./helpers";

test("admin pages and endpoints turn away signed-out visitors and customers", async ({ page, request }) => {
  // Signed out: the pages send you to sign in and bring you back afterwards
  for (const path of ["/admin/menu", "/admin/history"]) {
    await page.goto(path);
    await expect(page).toHaveURL(new RegExp(`/login\\?next=${encodeURIComponent(path)}`));
  }

  // The API itself: no token is 401, a customer's token is 403
  const email = uniqueEmail();
  const { token } = await apiRegister(request, "E2E Shopper", email);
  const asCustomer = { Authorization: `Bearer ${token}` };

  expect((await request.get(`${API_URL}/api/admin/products`)).status()).toBe(401);
  expect((await request.get(`${API_URL}/api/admin/products`, { headers: asCustomer })).status()).toBe(403);
  expect((await request.get(`${API_URL}/api/admin/orders`, { headers: asCustomer })).status()).toBe(403);
  expect(
    (
      await request.patch(`${API_URL}/api/admin/orders/${randomUUID()}/status`, {
        headers: asCustomer,
        data: { status: "Preparing" },
      })
    ).status(),
  ).toBe(403);

  // Signed in as a customer: the pages show "Admin access only"
  await signIn(page, email, PASSWORD);
  for (const path of ["/admin", "/admin/menu", "/admin/history"]) {
    await page.goto(path);
    await appReady(page);
    await expect(page.getByRole("heading", { name: "Admin access only" })).toBeVisible();
  }

  // The web app's proxy passes the customer's token on, and the API still says no
  expect((await page.request.get("/api/proxy/admin/products")).status()).toBe(403);

  // Only the order and admin areas are reachable through the proxy at all
  expect((await request.get(`${WEB_URL}/api/proxy/auth/me`)).status()).toBe(404);
});