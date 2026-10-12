import { expect, test } from "@playwright/test";
import { addToCart, fillDetails, next } from "./helpers";

test("checkout validates details, Enter advances, and reviewing never places the order", async ({ page }) => {
  // Regression test: the review step once submitted the order by itself.
  const orderRequests: string[] = [];
  page.on("request", (r) => {
    if (r.method() === "POST" && r.url().includes("/api/proxy/orders")) orderRequests.push(r.url());
  });

  await addToCart(page, "Flat White", "Small");
  await page.goto("/checkout");

  await next(page);
  await expect(page.getByText("Please enter your name.")).toBeVisible();
  await expect(page.getByText("Please enter your email.")).toBeVisible();
  await expect(page.getByText("Please enter your mobile number.")).toBeVisible();

  await fillDetails(page, "Guest Gary", "gary@example.com", "12345");
  await next(page);
  await expect(page.getByText(/valid Philippine mobile number/)).toBeVisible();

  // Pressing Enter in a field moves to the next step; spaces in the number are optional
  await page.getByLabel("Mobile number").fill("09171234567");
  await page.getByLabel("Mobile number").press("Enter");
  await expect(page.getByRole("heading", { name: "When will you pick it up?" })).toBeVisible();

  await page.getByRole("radio", { name: /Pay at the counter/ }).click();
  await next(page);
  await expect(page.getByRole("heading", { name: "Review your order" })).toBeVisible();

  await page.waitForTimeout(500); // gives a premature submit time to happen, if the bug returned
  expect(orderRequests).toHaveLength(0);
  await expect(page).toHaveURL(/\/checkout$/);
});

test("a guest can schedule a pickup, place a counter order, and track it", async ({ page }) => {
  await addToCart(page, "Flat White", "Large", 2);
  await page.goto("/checkout");

  await fillDetails(page, "Guest Gary", "gary@example.com");
  await next(page);

  // Pickup tomorrow at 10:00 AM (Philippine time) instead of "as soon as possible"
  await page.getByRole("radio", { name: /Pick a date and time/ }).click();
  await page.getByLabel("Day", { exact: true }).selectOption({ label: "Tomorrow" });
  await page.getByLabel("Time (Philippine time)").selectOption({ label: "10:00 AM" });
  await page.getByRole("radio", { name: /Pay at the counter/ }).click();
  await next(page);

  await expect(page.getByRole("heading", { name: "Review your order" })).toBeVisible();
  await page.getByRole("button", { name: /Place order/ }).click();

  await expect(page).toHaveURL(/\/orders\/[0-9a-f-]{36}$/);
  await expect(page.getByTestId("order-total")).toContainText("₱340"); // computed by the server
  await expect(page.getByTestId("order-status")).toHaveText("Order received");
  await expect(page.getByText(/10:00.*PHT/)).toBeVisible();
  await expect(page.getByText("Pay at the counter", { exact: true })).toBeVisible();
  await expect(page.getByText(/Save this page/)).toBeVisible();

  // The cart is cleared and the guest can find the order again from the header
  await expect(page.getByTestId("cart-count")).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Track order" })).toBeVisible();
});

test("large orders must be paid online", async ({ page }) => {
  await addToCart(page, "House Blend Beans", "1kg"); // ₱1,600 is over the ₱1,000 counter limit
  await page.goto("/checkout");
  await fillDetails(page, "Guest Gary", "gary@example.com");
  await next(page);

  await expect(page.getByRole("radio", { name: /Pay at the counter/ })).toBeDisabled();
  await expect(page.getByText(/Only for orders up to/)).toBeVisible();
});

test("falls back to pay at the counter when online payment is unavailable", async ({ page }) => {
  await addToCart(page, "Flat White", "Small");
  await page.goto("/checkout");
  await fillDetails(page, "Guest Gary", "gary@example.com");
  await next(page);

  await next(page); // "Pay online" is the default choice
  await page.getByRole("button", { name: /Pay .* online/ }).click();

  await expect(page.getByText("Online payment is temporarily unavailable.")).toBeVisible();
  await expect(page).toHaveURL(/\/checkout$/); // nothing was saved, and the form is intact

  await page.getByRole("button", { name: "Back" }).click();
  await page.getByRole("radio", { name: /Pay at the counter/ }).click();
  await next(page);
  await page.getByRole("button", { name: /Place order/ }).click();

  await expect(page).toHaveURL(/\/orders\/[0-9a-f-]{36}$/);
});