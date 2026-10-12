import { expect, type APIRequestContext, type Page } from "@playwright/test";

export const API_URL = process.env.E2E_API_URL ?? "http://localhost:5180";
export const ADMIN = { email: "admin@cafe.local", password: "E2eAdmin#12345" };
export const PASSWORD = "Passw0rd!x"; // meets every password rule
export const PHONE = "0917 123 4567";

export const uniqueEmail = (prefix = "shopper") =>
  `${prefix}-${Date.now()}-${Math.floor(Math.random() * 1_000_000)}@example.com`;

/**
 * The header skeleton disappears only after the app has hydrated and the session check has
 * finished. Waiting for that stops tests from clicking buttons that aren't wired up yet.
 */
export async function appReady(page: Page) {
  await expect(page.getByTestId("session-loading")).toBeHidden();
}

// ---------- UI helpers ----------

export async function addToCart(page: Page, product: string, variant: string, quantity = 1) {
  await page.goto("/");
  await appReady(page);

  const card = page.getByTestId("product-card").filter({ hasText: product });
  await card.getByRole("button", { name: /Choose options|Add to order/ }).click();

  const dialog = page.getByRole("dialog");
  await dialog.getByRole("radio", { name: new RegExp(variant) }).click();
  for (let i = 1; i < quantity; i++) {
    await dialog.getByRole("button", { name: `Increase quantity of ${product}` }).click();
  }
  await dialog.getByRole("button", { name: /Add to order/ }).click();
  await expect(dialog).toBeHidden();
}

export async function register(page: Page, name: string, email: string, password = PASSWORD) {
  await page.goto("/register");
  await appReady(page);
  await page.getByLabel("Full name").fill(name);
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Create account" }).click();
  await expect(page.getByRole("button", { name: "Sign out" })).toBeVisible();
}

export async function signIn(page: Page, email: string, password: string) {
  await page.goto("/login");
  await appReady(page);
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("button", { name: "Sign out" })).toBeVisible();
}

export async function fillDetails(page: Page, name: string, email: string, phone = PHONE) {
  await page.getByLabel("Name", { exact: true }).fill(name);
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Mobile number").fill(phone);
}

export async function next(page: Page) {
  await page.getByRole("button", { name: "Continue" }).click();
}

// ---------- API helpers (quick setup that doesn't need the browser) ----------

export async function apiRegister(request: APIRequestContext, name: string, email: string, password = PASSWORD) {
  const response = await request.post(`${API_URL}/api/auth/register`, { data: { name, email, password } });
  expect(response.status()).toBe(201);
  return (await response.json()) as { token: string };
}

export async function apiLogin(request: APIRequestContext, email: string, password: string) {
  const response = await request.post(`${API_URL}/api/auth/login`, { data: { email, password } });
  expect(response.status()).toBe(200);
  return ((await response.json()) as { token: string }).token;
}

/** Places a ₱130 pay-at-counter order. Returns the raw response so tests can check the status. */
export async function apiPlaceOrder(request: APIRequestContext, token?: string) {
  const menu = (await (await request.get(`${API_URL}/api/products`)).json()) as { id: number; name: string }[];
  const flatWhite = menu.find((p) => p.name === "Flat White");
  if (!flatWhite) throw new Error("Seed data is missing 'Flat White'.");

  const detail = (await (await request.get(`${API_URL}/api/products/${flatWhite.id}`)).json()) as {
    variants: { id: number }[];
  };

  return request.post(`${API_URL}/api/orders`, {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    data: {
      customerName: "API Customer",
      customerEmail: "api-customer@example.com",
      customerPhone: "09171234567",
      pickupTime: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      paymentMethod: "PayAtCounter",
      items: [{ variantId: detail.variants[0].id, quantity: 1 }],
    },
  });
}