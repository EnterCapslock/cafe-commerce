import { expect, test } from "@playwright/test";
import { apiPlaceOrder, apiRegister, appReady, uniqueEmail } from "./helpers";

test("a guest finds their order again with the order code and email", async ({ page, request }) => {
  const email = uniqueEmail("finder");
  const response = await apiPlaceOrder(request, undefined, { email });
  expect(response.status()).toBe(201);
  const { id } = (await response.json()) as { id: string };
  const code = id.slice(0, 8);

  await page.goto("/find-order");
  await appReady(page);

  // Typed sloppily: lower case, a space in the middle, and an upper-case email
  await page.getByLabel("Order code").fill(`#${code.slice(0, 4)} ${code.slice(4)}`.toLowerCase());
  await page.getByLabel("Email used at checkout").fill(email.toUpperCase());
  await page.getByRole("button", { name: "Find my order" }).click();

  await expect(page).toHaveURL(new RegExp(`/orders/${id}$`));
  await expect(page.getByTestId("order-code")).toHaveText(code.toUpperCase());
  await expect(page.getByRole("link", { name: "Track order" })).toBeVisible();   // remembered on this device
});

test("a lookup that doesn't match says so without revealing why, and account orders stay private", async ({ page, request }) => {
  const guestEmail = uniqueEmail("guest");
  const guest = await apiPlaceOrder(request, undefined, { email: guestEmail });
  const guestId = ((await guest.json()) as { id: string }).id;

  const accountEmail = uniqueEmail("account");
  const { token } = await apiRegister(request, "E2E Shopper", accountEmail);
  const account = await apiPlaceOrder(request, token, { email: accountEmail });
  const accountId = ((await account.json()) as { id: string }).id;

  await page.goto("/find-order");
  await appReady(page);

  async function lookup(code: string, email: string) {
    await page.getByLabel("Order code").fill(code);
    await page.getByLabel("Email used at checkout").fill(email);
    await page.getByRole("button", { name: "Find my order" }).click();
  }

  // Right code, wrong email
  await lookup(guestId.slice(0, 8), uniqueEmail("wrong"));
  await expect(page.getByText("We couldn't find an order with that code and email.")).toBeVisible();
  await expect(page).toHaveURL(/\/find-order$/);

  // An order that belongs to an account can't be found this way, even with the right code and email
  await lookup(accountId.slice(0, 8), accountEmail);
  await expect(page.getByText("We couldn't find an order with that code and email.")).toBeVisible();
  await expect(page).toHaveURL(/\/find-order$/);

  // A code that can't be valid is caught before anything is sent
  await lookup("123", guestEmail);
  await expect(page.getByText("The order code is 8 letters and numbers")).toBeVisible();
});