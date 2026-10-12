import { expect, test } from "@playwright/test";
import { ADMIN, apiLogin, apiPlaceOrder, apiRegister, PASSWORD, signIn, uniqueEmail } from "./helpers";

test("the admin area is guarded for signed-out visitors and customers", async ({ page, request }) => {
  await page.goto("/admin");
  await expect(page).toHaveURL(/\/login\?next=%2Fadmin/);

  const email = uniqueEmail();
  await apiRegister(request, "E2E Shopper", email);
  await signIn(page, email, PASSWORD);

  await page.goto("/admin");
  await expect(page.getByRole("heading", { name: "Admin access only" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Admin", exact: true })).toHaveCount(0);
});

test("admins can't shop but can view customer orders", async ({ page, request }) => {
  // The API refuses orders from an admin token
  const adminToken = await apiLogin(request, ADMIN.email, ADMIN.password);
  expect((await apiPlaceOrder(request, adminToken)).status()).toBe(403);

  // A customer's order, created through the API
  const { token } = await apiRegister(request, "E2E Shopper", uniqueEmail());
  const response = await apiPlaceOrder(request, token);
  expect(response.status()).toBe(201);
  const { id } = (await response.json()) as { id: string };

  await signIn(page, ADMIN.email, ADMIN.password);
  await expect(page.getByRole("link", { name: "Admin", exact: true })).toBeVisible();
  await expect(page.getByRole("link", { name: /^Cart/ })).toHaveCount(0);

  await page.goto("/admin");
  await expect(page.getByRole("heading", { name: "Live orders" })).toBeVisible();

  await page.goto("/checkout");
  await expect(page.getByRole("heading", { name: /place orders/ })).toBeVisible();

  // Staff can open the customer's private order, in an "Admin view"
  await page.goto(`/orders/${id}`);
  await expect(page.getByText("Admin view", { exact: true })).toBeVisible();
  await expect(page.getByText("09171234567")).toBeVisible();
  await expect(page.getByRole("link", { name: "Back to admin" })).toBeVisible();
});