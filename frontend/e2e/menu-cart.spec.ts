import { expect, test } from "@playwright/test";
import { addToCart, appReady } from "./helpers";

test("the menu loads and filters by category", async ({ page }) => {
  await page.goto("/");
  await appReady(page);

  // Other tests add temporary items, so assert "at least the 15 seeded items", never an exact total.
  const cards = page.getByTestId("product-card");
  await expect.poll(() => cards.count()).toBeGreaterThanOrEqual(15);

  await page.getByRole("link", { name: "Coffee", exact: true }).click();
  await expect(page).toHaveURL(/category=Coffee/);
  await expect(cards).toHaveCount(7); // test items are always Merchandise

  await page.getByRole("link", { name: "All", exact: true }).click();
  await expect.poll(() => cards.count()).toBeGreaterThanOrEqual(15);
});

test("the cart survives a refresh and totals use peso prices", async ({ page }) => {
  await addToCart(page, "Flat White", "Large", 2);
  await expect(page.getByTestId("cart-count")).toHaveText("2");

  await page.reload();
  await expect(page.getByTestId("cart-count")).toHaveText("2");

  await page.getByRole("link", { name: /^Cart/ }).click();
  await expect(page.getByTestId("cart-line")).toHaveCount(1);
  await expect(page.getByTestId("cart-subtotal")).toContainText("₱340"); // 2 × ₱170

  await page.getByRole("button", { name: "Increase quantity of Flat White (Large)" }).click();
  await expect(page.getByTestId("cart-subtotal")).toContainText("₱510");

  await page.getByRole("button", { name: "Remove Flat White (Large)" }).click();
  await expect(page.getByText("Your cart is empty")).toBeVisible();
});