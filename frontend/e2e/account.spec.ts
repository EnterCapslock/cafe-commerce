import { expect, test } from "@playwright/test";
import { addToCart, appReady, apiRegister, next, PASSWORD, PHONE, register, uniqueEmail } from "./helpers";

test("a customer registers, orders, sees it in My orders, and it stays private", async ({ page, browser }) => {
  const email = uniqueEmail();
  await register(page, "E2E Shopper", email);

  // The session cookie is invisible to JavaScript
  const cookie = (await page.context().cookies()).find((c) => c.name === "cafe_session");
  expect(cookie?.httpOnly).toBe(true);
  expect(cookie?.sameSite).toBe("Lax");
  expect(await page.evaluate(() => document.cookie)).not.toContain("cafe_session");

  await addToCart(page, "Latte", "Medium");
  await page.goto("/checkout");

  // Name and email come from the account, and the order is saved to it
  await expect(page.getByLabel("Name", { exact: true })).toHaveValue("E2E Shopper");
  await expect(page.getByLabel("Email", { exact: true })).toHaveValue(email);
  await expect(page.getByText(/saved to your account/)).toBeVisible();

  await page.getByLabel("Mobile number").fill(PHONE);
  await next(page);
  await page.getByRole("radio", { name: /Pay at the counter/ }).click();
  await next(page);
  await page.getByRole("button", { name: /Place order/ }).click();

  await expect(page).toHaveURL(/\/orders\/[0-9a-f-]{36}$/);
  const orderUrl = page.url();
  await expect(page.getByText(/Save this page/)).toHaveCount(0); // that card is for guests

  await page.getByRole("link", { name: "My orders" }).click();
  await expect(page.getByTestId("my-order")).toHaveCount(1);

  // Someone who isn't signed in can't open an account order by link
  const stranger = await browser.newContext();
  const strangerPage = await stranger.newPage();
  await strangerPage.goto(orderUrl);
  await expect(strangerPage.getByRole("heading", { name: /find that order/ })).toBeVisible();
  await stranger.close();

  // After signing out, account pages send you to sign in
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page.getByRole("banner").getByRole("link", { name: "Sign in" })).toBeVisible();
  await page.goto("/account/orders");
  await expect(page).toHaveURL(/\/login\?next=%2Faccount%2Forders/);
});

test("sign-in shows a clear error for a wrong password", async ({ page, request }) => {
  const email = uniqueEmail();
  await apiRegister(request, "E2E Shopper", email, PASSWORD);

  await page.goto("/login");
  await appReady(page);
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill("Wrong#Pass1");
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page.getByText("Invalid email or password.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Sign out" })).toHaveCount(0);
});