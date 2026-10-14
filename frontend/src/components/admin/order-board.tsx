"use client";

import { RefreshCw } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";
import { CancelOrderDialog } from "@/components/admin/cancel-order-dialog";
import { nextStep, OrderCard } from "@/components/admin/order-card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { SITE_NAME } from "@/lib/config";
import { useNow } from "@/lib/hooks/use-now";
import { useOrderBoard } from "@/lib/hooks/use-order-board";
import type { OrderDto, OrderStatus } from "@/types/api";

const COLUMNS: { status: OrderStatus; title: string; empty: string }[] = [
  { status: "Pending", title: "New", empty: "No new orders" },
  { status: "Preparing", title: "Preparing", empty: "Nothing being prepared" },
  { status: "ReadyForPickup", title: "Ready for pickup", empty: "Nothing waiting for pickup" },
];

const clock = new Intl.DateTimeFormat("en-PH", { timeZone: "Asia/Manila", timeStyle: "medium" });

function matches(order: OrderDto, query: string): boolean {
  const q = query.trim().toLowerCase().replace(/^#/, "");
  if (!q) return true;
  return (
    order.id.toLowerCase().startsWith(q) ||
    order.customerName.toLowerCase().includes(q) ||
    order.customerEmail.toLowerCase().includes(q) ||
    order.customerPhone.replace(/\s/g, "").includes(q.replace(/\s/g, ""))
  );
}

export function OrderBoard() {
  const { state, fresh, busy, stale, updatedAt, refresh, changeStatus } = useOrderBoard();
  const now = useNow();
  const [query, setQuery] = useState("");
  const [cancelling, setCancelling] = useState<OrderDto | null>(null);

  const orders = state.status === "ready" ? state.orders : [];
  const newCount = orders.filter((o) => o.status === "Pending").length;

  // Show the number of new orders in the browser tab, so a barista on another tab notices.
  useEffect(() => {
    const previous = document.title;
    document.title = newCount > 0 ? `(${newCount}) Orders · ${SITE_NAME}` : `Orders · ${SITE_NAME}`;
    return () => {
      document.title = previous;
    };
  }, [newCount]);

  if (state.status === "loading") {
    return (
      <div className="space-y-4" aria-busy="true">
        <h1 className="text-3xl font-semibold tracking-tight">Live orders</h1>
        <p role="status" className="text-sm text-muted-foreground">Loading orders…</p>
        <div className="grid gap-6 lg:grid-cols-3">
          {[0, 1, 2].map((i) => <div key={i} className="h-64 animate-pulse rounded-xl bg-muted" />)}
        </div>
      </div>
    );
  }

  if (state.status === "unauthorized") {
    return (
      <div className="mx-auto max-w-md space-y-4 py-16 text-center">
        <h1 className="text-2xl font-semibold">Your session has ended</h1>
        <p className="text-muted-foreground">Please sign in again to keep managing orders.</p>
        <Button asChild>
          <Link href="/login?next=/admin">Sign in</Link>
        </Button>
      </div>
    );
  }

  if (state.status === "error") {
    return (
      <div className="mx-auto max-w-md space-y-4 py-16 text-center">
        <h1 className="text-2xl font-semibold">We couldn&apos;t load the orders</h1>
        <p className="text-muted-foreground">The server may be waking up. We&apos;ll keep trying automatically.</p>
        <Button variant="outline" onClick={() => void refresh()}>Try again</Button>
      </div>
    );
  }

  const visible = orders.filter((o) => matches(o, query));

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="space-y-1">
          <h1 className="text-3xl font-semibold tracking-tight">Live orders</h1>
          <p className="flex items-center gap-2 text-sm text-muted-foreground">
            <span
              aria-hidden
              className={stale ? "size-2 rounded-full bg-amber-500" : "size-2 rounded-full bg-green-500"}
            />
            {stale ? "Reconnecting…" : "Live"}
            {updatedAt !== null && <span>· Updated {clock.format(updatedAt)}</span>}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Input
            type="search"
            aria-label="Search active orders"
            placeholder="Search name, phone, or order code"
            className="w-64"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
          <Button variant="outline" size="icon" aria-label="Refresh orders" onClick={() => void refresh()}>
            <RefreshCw aria-hidden />
          </Button>
        </div>
      </div>

      <p role="status" className="sr-only">
        {newCount} new {newCount === 1 ? "order" : "orders"}
      </p>

      <div className="grid items-start gap-6 lg:grid-cols-3">
        {COLUMNS.map((column) => {
          const inColumn = visible.filter((o) => o.status === column.status);
          return (
            <section
              key={column.status}
              aria-labelledby={`col-${column.status}`}
              data-testid={`board-column-${column.status}`}
              className="space-y-3"
            >
              <h2 id={`col-${column.status}`} className="flex items-center gap-2 font-medium">
                {column.title}
                <Badge variant="secondary">{inColumn.length}</Badge>
              </h2>
              {inColumn.length === 0 ? (
                <p className="rounded-xl border border-dashed p-6 text-center text-sm text-muted-foreground">
                  {column.empty}
                </p>
              ) : (
                <ul className="space-y-3">
                  {inColumn.map((order) => (
                    <OrderCard
                      key={order.id}
                      order={order}
                      now={now}
                      isNew={fresh.has(order.id)}
                      busy={busy.has(order.id)}
                      onAdvance={(o) => {
                        const step = nextStep(o);
                        if (step) void changeStatus(o.id, step.target);
                      }}
                      onCancel={setCancelling}
                    />
                  ))}
                </ul>
              )}
            </section>
          );
        })}
      </div>

      <CancelOrderDialog
        order={cancelling}
        onClose={() => setCancelling(null)}
        onConfirm={async (order) => {
          await changeStatus(order.id, "Cancelled");
        }}
      />
    </div>
  );
}