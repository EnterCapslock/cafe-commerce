import { expect, type APIRequestContext, type Browser, type BrowserContext, type Page } from "@playwright/test";

export const API_URL = process.env.E2E_API_URL ?? "http://localhost:5180";
export const WEB_URL = process.env.E2E_WEB_URL ?? "http://localhost:3100";
export const ADMIN = { email: "admin@cafe.local", password: "E2eAdmin#12345" };
export const PASSWORD = "Passw0rd!x"; // meets every password rule
export const PHONE = "0917 123 4567";

export const uniqueEmail = (prefix = "shopper") =>
  `${prefix}-${Date.now()}-${Math.floor(Math.random() * 1_000_000)}@example.com`;

/** A readable name that's unique per call, for finding "my" order or item among shared test data. */
export const uniqueName = (prefix: string) =>
  `${prefix} ${Date.now().toString(36)}${Math.floor(Math.random() * 1296).toString(36)}`;

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

// ---------- Separate browsers (a customer and a barista at the same time) ----------

export interface Session {
  context: BrowserContext;
  page: Page;
}

/** A fresh, signed-out browser. */
export async function newVisitor(browser: Browser): Promise<Session> {
  const context = await browser.newContext({ baseURL: WEB_URL });
  return { context, page: await context.newPage() };
}

/** A fresh browser, signed in as the admin. */
export async function adminPage(browser: Browser): Promise<Session> {
  const session = await newVisitor(browser);
  await signIn(session.page, ADMIN.email, ADMIN.password);
  return session;
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

export const apiAdminToken = (request: APIRequestContext) => apiLogin(request, ADMIN.email, ADMIN.password);

/** Places a ₱130 pay-at-counter order. Returns the raw response so tests can check the status. */
export async function apiPlaceOrder(
  request: APIRequestContext,
  token?: string,
  options: { name?: string; pickupMinutes?: number } = {},
) {
  const menu = (await (await request.get(`${API_URL}/api/products`)).json()) as { id: number; name: string }[];
  const flatWhite = menu.find((p) => p.name === "Flat White");
  if (!flatWhite) throw new Error("Seed data is missing 'Flat White'.");

  const detail = (await (await request.get(`${API_URL}/api/products/${flatWhite.id}`)).json()) as {
    variants: { id: number }[];
  };

  return request.post(`${API_URL}/api/orders`, {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    data: {
      customerName: options.name ?? "API Customer",
      customerEmail: "api-customer@example.com",
      customerPhone: "09171234567",
      pickupTime: new Date(Date.now() + (options.pickupMinutes ?? 60) * 60_000).toISOString(),
      paymentMethod: "PayAtCounter",
      items: [{ variantId: detail.variants[0].id, quantity: 1 }],
    },
  });
}

/**
 * A guest pay-at-counter order with the soonest pickup the API allows (16 minutes away).
 * Tests that leave their order active should cancel it afterwards with apiCancelOrder, because
 * the board shows only the 100 soonest-pickup orders.
 */
export async function apiPlaceBoardOrder(request: APIRequestContext, name: string): Promise<string> {
  const response = await apiPlaceOrder(request, undefined, { name, pickupMinutes: 16 });
  expect(response.status()).toBe(201);
  return ((await response.json()) as { id: string }).id;
}

export interface TestProduct {
  id: number;
  name: string;
  description: string;
  category: string;
  variants: { id: number; name: string; price: number }[];
}

export async function apiCreateProduct(
  request: APIRequestContext,
  token: string,
  product: { name: string; variants: { name: string; price: number }[] },
): Promise<TestProduct> {
  const response = await request.post(`${API_URL}/api/admin/products`, {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      name: product.name,
      description: "Created by an end-to-end test.",
      category: "Merchandise", // keeps the seeded Coffee/Food counts untouched
      isActive: true,
      variants: product.variants.map((v) => ({ ...v, isAvailable: true })),
    },
  });
  expect(response.status()).toBe(201);
  return (await response.json()) as TestProduct;
}

export async function apiUpdateProduct(
  request: APIRequestContext,
  token: string,
  product: TestProduct,
  changes: { isActive?: boolean; price?: number },
) {
  const response = await request.put(`${API_URL}/api/admin/products/${product.id}`, {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      name: product.name,
      description: product.description,
      category: product.category,
      isActive: changes.isActive ?? true,
      variants: product.variants.map((v) => ({
        id: v.id,
        name: v.name,
        price: changes.price ?? v.price,
        isAvailable: true,
      })),
    },
  });
  expect(response.status()).toBe(200);
}

/** Best-effort cleanup: never fails a test. */
export async function apiDeleteProduct(request: APIRequestContext, token: string, id: number) {
  await request.delete(`${API_URL}/api/admin/products/${id}`, { headers: { Authorization: `Bearer ${token}` } });
}

/** Best-effort cleanup: never fails a test. Keeps leftover orders from piling up on the board. */
export async function apiCancelOrder(request: APIRequestContext, token: string, orderId: string) {
  await request.patch(`${API_URL}/api/admin/orders/${orderId}/status`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { status: "Cancelled" },
  });
}