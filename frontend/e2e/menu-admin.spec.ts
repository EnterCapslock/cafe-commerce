import { expect, test } from "@playwright/test";
import {
  API_URL,
  adminPage,
  apiAdminToken,
  apiCreateProduct,
  apiDeleteProduct,
  apiPlaceOrder,
  apiUpdateProduct,
  appReady,
  newVisitor,
  uniqueName,
} from "./helpers";

test("an admin creates an item with variants, and customers see it and its availability", async ({ browser, request }) => {
  const token = await apiAdminToken(request);
  const name = uniqueName("E2E Mug");
  let productId: number | undefined;

  try {
    const staff = await adminPage(browser);
    const admin = staff.page;

    await admin.goto("/admin/menu/new");
    await appReady(admin);
    await expect(admin.getByRole("heading", { name: "New menu item" })).toBeVisible();

    // The form checks input before anything reaches the server
    await admin.getByRole("button", { name: "Create item" }).click();
    await expect(admin.getByRole("alert").filter({ hasText: "Enter a name." }).first()).toBeVisible();
    await expect(admin.getByRole("alert").filter({ hasText: "Enter a description." })).toBeVisible();

    await admin.getByLabel("Name", { exact: true }).fill(name);
    await admin.getByLabel("Description").fill("Made by an end-to-end test.");
    await admin.getByLabel("Category").selectOption("Merchandise");

    // Exactly one variant row is on screen. If the form ever renders a visible duplicate, this fails.
    await expect(admin.getByTestId("variant-row").filter({ visible: true })).toHaveCount(1);

    await admin.getByRole("textbox", { name: "Variant 1 name" }).fill("Small");
    await admin.getByRole("textbox", { name: "Variant 1 price" }).fill("1.234"); // too many decimals
    await admin.getByRole("button", { name: "Add a variant" }).click();
    await admin.getByRole("textbox", { name: "Variant 2 name" }).fill("Large");
    await admin.getByRole("textbox", { name: "Variant 2 price" }).fill("250.50");
    await admin.getByRole("button", { name: "Create item" }).click();
    await expect(admin.getByRole("alert").filter({ hasText: /up to 2 decimals/i })).toBeVisible();

    await admin.getByRole("textbox", { name: "Variant 1 price" }).fill("200");
    await admin.getByRole("button", { name: "Create item" }).click();
    await expect(admin).toHaveURL(/\/admin\/menu\/\d+$/);
    await expect(admin.getByRole("heading", { name: `Edit ${name}` })).toBeVisible();
    productId = Number(new URL(admin.url()).pathname.split("/").pop());

    // A customer sees it. The menu page caches for up to 30 seconds, so retry briefly.
    const visitor = await newVisitor(browser);
    const card = visitor.page.getByTestId("product-card").filter({ hasText: name });
    await expect(async () => {
      await visitor.page.goto("/?category=Merchandise");
      await expect(card).toBeVisible({ timeout: 2_000 });
    }).toPass({ timeout: 45_000, intervals: [1_000, 3_000, 5_000] });
    await expect(card).toContainText("From ₱200");

    await appReady(visitor.page);
    await card.getByRole("button", { name: "Choose options" }).click();
    const dialog = visitor.page.getByRole("dialog");
    await expect(dialog.getByRole("radio", { name: /Large/ })).toBeEnabled();

    // The admin marks "Large" sold out with one click
    await admin.goto("/admin/menu");
    await admin.getByRole("searchbox", { name: "Search items" }).fill(name);
    const row = admin.getByTestId("menu-row").filter({ hasText: name });
    const large = row.getByRole("button", { name: `${name} Large available` });
    await large.click();
    await expect(large).toHaveAttribute("aria-pressed", "false");

    // The product dialog loads fresh data every time it opens, so the customer sees it right away
    await visitor.page.keyboard.press("Escape");
    await expect(dialog).toBeHidden();
    await card.getByRole("button", { name: "Choose options" }).click();
    await expect(visitor.page.getByRole("dialog").getByRole("radio", { name: /Large/ })).toBeDisabled();
    await expect(visitor.page.getByRole("dialog").getByRole("radio", { name: /Small/ })).toBeEnabled();

    await visitor.context.close();
    await staff.context.close();
  } finally {
    if (productId) await apiDeleteProduct(request, token, productId);
  }
});

test("archiving hides an item, ordered items can't be deleted, and unused ones can", async ({ browser, request }) => {
  const token = await apiAdminToken(request);
  const name = uniqueName("E2E Archive");
  const product = await apiCreateProduct(request, token, { name, variants: [{ name: "Regular", price: 90 }] });
  const publicNames = async () =>
    ((await (await request.get(`${API_URL}/api/products`)).json()) as { name: string }[]).map((p) => p.name);

  try {
    // Make sure "Flat White" has at least one past order
    expect((await apiPlaceOrder(request)).status()).toBe(201);

    const staff = await adminPage(browser);
    const page = staff.page;
    await page.goto("/admin/menu");
    await appReady(page);
    await page.getByRole("searchbox", { name: "Search items" }).fill(name);
    const row = page.getByTestId("menu-row").filter({ hasText: name });

    // Archive: leaves the menu, stays in the admin list under "Archived"
    await expect(row).toHaveCount(1);
    await row.getByRole("button", { name: `Archive ${name}` }).click();
    await expect(row).toHaveCount(0);
    expect(await publicNames()).not.toContain(name);

    await page.getByLabel("Filter by visibility").selectOption("archived");
    await expect(row).toContainText("Archived");

    // Restore: back on the menu
    await row.getByRole("button", { name: `Restore ${name}` }).click();
    await expect(row).toHaveCount(0);
    expect(await publicNames()).toContain(name);

    // An item with past orders can't be deleted: the dialog explains and offers archiving.
    // (We only read the message and close it. Archiving Flat White would disturb other tests.)
    await page.getByLabel("Filter by visibility").selectOption("active");
    await page.getByRole("searchbox", { name: "Search items" }).fill("Flat White");
    await page.getByRole("button", { name: "Delete Flat White" }).click();
    const dialog = page.getByRole("dialog");
    await dialog.getByRole("button", { name: "Delete", exact: true }).click();
    await expect(dialog.getByRole("heading", { name: "This item can't be deleted" })).toBeVisible();
    await expect(dialog.getByRole("button", { name: "Archive instead" })).toBeVisible();
    await page.keyboard.press("Escape");
    await expect(dialog).toBeHidden();

    // An item that was never ordered can be deleted for good
    await page.getByRole("searchbox", { name: "Search items" }).fill(name);
    await row.getByRole("button", { name: `Delete ${name}` }).click();
    await page.getByRole("dialog").getByRole("button", { name: "Delete", exact: true }).click();
    await expect(row).toHaveCount(0);

    const gone = await request.get(`${API_URL}/api/admin/products/${product.id}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(gone.status()).toBe(404);

    await staff.context.close();
  } finally {
    await apiDeleteProduct(request, token, product.id); // a no-op if the test already deleted it
  }
});

test("the cart warns about items that were removed or repriced", async ({ page, request }) => {
  const token = await apiAdminToken(request);
  const removed = await apiCreateProduct(request, token, {
    name: uniqueName("E2E Removed"),
    variants: [{ name: "Regular", price: 100 }],
  });
  const repriced = await apiCreateProduct(request, token, {
    name: uniqueName("E2E Repriced"),
    variants: [{ name: "Regular", price: 120 }],
  });

  const line = (p: typeof removed) => ({
    variantId: p.variants[0].id,
    productId: p.id,
    productName: p.name,
    variantName: "Regular",
    unitPrice: p.variants[0].price,
    quantity: 1,
  });

  try {
    // Put both items in the cart directly, because the menu page caches and new items may not show yet
    await page.goto("/");
    await appReady(page);
    await page.evaluate(
      ([key, value]) => localStorage.setItem(key, value),
      ["cafe-cart", JSON.stringify({ state: { items: [line(removed), line(repriced)] }, version: 1 })],
    );

    // Meanwhile the admin archives one and changes the other's price
    await apiUpdateProduct(request, token, removed, { isActive: false });
    await apiUpdateProduct(request, token, repriced, { price: 150 });

    await page.goto("/cart");
    const notice = page.getByTestId("cart-check-notice");
    await expect(notice).toBeVisible();
    await expect(notice).toContainText(`${removed.name} (Regular) is no longer available.`);
    await expect(notice).toContainText(`${repriced.name} (Regular) is now ₱150 (was ₱120).`);

    await notice.getByRole("button", { name: "Update cart" }).click();
    await expect(notice).toHaveCount(0);
    await expect(page.getByTestId("cart-line")).toHaveCount(1);
    await expect(page.getByTestId("cart-subtotal")).toContainText("₱150");
  } finally {
    await apiDeleteProduct(request, token, removed.id);
    await apiDeleteProduct(request, token, repriced.id);
  }
});