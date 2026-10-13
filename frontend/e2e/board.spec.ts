import { expect, test, type Page } from "@playwright/test";
import {
  adminPage,
  apiAdminToken,
  apiCancelOrder,
  apiPlaceBoardOrder,
  newVisitor,
  uniqueName,
} from "./helpers";

// These tests wait on the board's 10-second refresh, so give them more room.
test.describe.configure({ timeout: 90_000 });

const inColumn = (page: Page, status: string, name: string) =>
  page.getByTestId(`board-column-${status}`).getByTestId("board-order").filter({ hasText: name });

test("an order moves from placed to completed, and the customer follows along", async ({ browser, request }) => {
  const name = uniqueName("Lifecycle");
  const id = await apiPlaceBoardOrder(request, name);

  // The customer's own browser, on their tracking page
  const customer = await newVisitor(browser);
  await customer.page.goto(`/orders/${id}`);
  await expect(customer.page.getByTestId("order-status")).toHaveText("Order received");

  // A barista on another browser
  const staff = await adminPage(browser);
  const page = staff.page;
  await page.goto("/admin");
  await expect(inColumn(page, "Pending", name)).toBeVisible();
  const card = page.getByTestId("board-order").filter({ hasText: name });

  await card.getByRole("button", { name: /^Start preparing/ }).click();
  await expect(inColumn(page, "Preparing", name)).toBeVisible();

  await customer.page.reload();
  await expect(customer.page.getByTestId("order-status")).toHaveText("Being prepared");

  await card.getByRole("button", { name: /^Mark ready/ }).click();
  await expect(inColumn(page, "ReadyForPickup", name)).toBeVisible();

  // No refresh: the tracking page updates on its own (it checks every 10 seconds)
  await expect(customer.page.getByTestId("order-status")).toHaveText("Ready for pickup", { timeout: 25_000 });

  // A pay-at-counter order is paid when the barista collects the money
  await card.getByRole("button", { name: /^Collect ₱130/ }).click();
  await expect(card).toHaveCount(0);

  await customer.page.reload();
  await expect(customer.page.getByTestId("order-status")).toHaveText("Completed");
  await expect(customer.page.getByText("Payment received")).toBeVisible();

  // It's in the history, marked as paid at the counter
  await page.goto("/admin/history");
  await page.getByRole("searchbox", { name: "Search order history" }).fill(name);
  await page.getByRole("searchbox", { name: "Search order history" }).press("Enter");
  const row = page.getByTestId("history-order").filter({ hasText: name });
  await expect(row).toBeVisible();
  await expect(row).toContainText("paid at counter");

  await customer.context.close();
  await staff.context.close();
});

test("new orders appear on the board without a refresh", async ({ browser, request }) => {
  const token = await apiAdminToken(request);
  const staff = await adminPage(browser);
  const page = staff.page;
  let orderId: string | undefined;

  try {
    await page.goto("/admin");

    // The first load decides what counts as "already there", so wait for it before placing the order.
    await expect(page.getByTestId("board-column-Pending")).toBeVisible();

    const name = uniqueName("Live");
    orderId = await apiPlaceBoardOrder(request, name);

    // The board refreshes every 10 seconds. Our order's card appears, highlighted as new.
    const card = page.getByTestId("board-order").filter({ hasText: name });
    await expect(card).toBeVisible({ timeout: 25_000 });
    await expect(card.getByText("New", { exact: true })).toBeVisible();

    // The alert says "New order from <name>", or "N new orders" when other tests place orders
    // at the same moment (they run in parallel), so accept either wording.
    await expect(page.getByText(/New order from .+|\d+ new orders/).first()).toBeVisible();
  } finally {
    if (orderId) await apiCancelOrder(request, token, orderId);
    await staff.context.close();
  }
});

test("two baristas can't both advance the same order", async ({ browser, request }) => {
  const token = await apiAdminToken(request);
  const name = uniqueName("Race");
  const id = await apiPlaceBoardOrder(request, name);

  const a = await adminPage(browser);
  const b = await adminPage(browser);

  try {
    for (const { page } of [a, b]) {
      await page.goto("/admin");
      await expect(inColumn(page, "Pending", name)).toBeVisible();
    }

    // Keep barista B's board from refreshing on its own, so it stays out of date
    let blockPolling = true;
    await b.page.route(/\/api\/proxy\/admin\/orders(\?.*)?$/, (route) =>
      blockPolling && route.request().method() === "GET" ? route.abort() : route.continue(),
    );

    // Barista A starts preparing it
    await a.page
      .getByTestId("board-order")
      .filter({ hasText: name })
      .getByRole("button", { name: /^Start preparing/ })
      .click();
    await expect(inColumn(a.page, "Preparing", name)).toBeVisible();

    // Barista B still sees it as new and clicks too: the API refuses, and B's board re-syncs
    blockPolling = false;
    await b.page
      .getByTestId("board-order")
      .filter({ hasText: name })
      .getByRole("button", { name: /^Start preparing/ })
      .click();

    await expect(b.page.getByText(/can't be changed|just updated/).first()).toBeVisible();
    await expect(inColumn(b.page, "Preparing", name)).toBeVisible();
  } finally {
    await apiCancelOrder(request, token, id);
    await a.context.close();
    await b.context.close();
  }
});

test("cancelling an order asks first, and the customer and history show it", async ({ browser, request }) => {
  const name = uniqueName("Cancel");
  const id = await apiPlaceBoardOrder(request, name);

  const staff = await adminPage(browser);
  const page = staff.page;
  await page.goto("/admin");
  const card = page.getByTestId("board-order").filter({ hasText: name });

  await card.getByRole("button", { name: /^Cancel order/ }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toContainText("The customer will see their order as cancelled");

  // Keeping the order leaves it on the board
  await dialog.getByRole("button", { name: "Keep order" }).click();
  await expect(card).toBeVisible();

  await card.getByRole("button", { name: /^Cancel order/ }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Cancel order", exact: true }).click();
  await expect(card).toHaveCount(0);

  const customer = await newVisitor(browser);
  await customer.page.goto(`/orders/${id}`);
  await expect(customer.page.getByTestId("order-status")).toHaveText("Cancelled");
  await expect(customer.page.getByText("Order cancelled", { exact: true })).toBeVisible();

  await page.goto("/admin/history");
  await page.getByRole("button", { name: "Cancelled" }).click();
  await page.getByRole("searchbox", { name: "Search order history" }).fill(name);
  await page.getByRole("searchbox", { name: "Search order history" }).press("Enter");
  await expect(page.getByTestId("history-order").filter({ hasText: name })).toBeVisible();

  await customer.context.close();
  await staff.context.close();
});