import { expect, test } from "@playwright/test";
import { addToCart, appReady } from "./helpers";

test("the menu loads and filters by category", async ({ page }) => {
  await page.goto("/");
  await appReady(page);
  await expect(page.getByTestId("product-card")).toHaveCount(15);

  await page.getByRole("link", { name: "Coffee", exact: true }).click();
  await expect(page).toHaveURL(/category=Coffee/);
  await expect(page.getByTestId("product-card")).toHaveCount(7);

  await page.getByRole("link", { name: "All", exact: true }).click();
  await expect(page.getByTestId("product-card")).toHaveCount(15);
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