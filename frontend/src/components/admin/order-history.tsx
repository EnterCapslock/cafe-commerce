"use client";

import Link from "next/link";
import { useEffect, useState, type FormEvent } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { api } from "@/lib/api/client";
import { formatPeso } from "@/lib/format";
import { formatPhtShort } from "@/lib/time";
import type { OrderDto } from "@/types/api";

type HistoryStatus = "Completed" | "Cancelled";
type Result = { key: string; orders: OrderDto[] } | { key: string; error: true };

function describe(order: OrderDto): string {
  if (order.status === "Cancelled") {
    return order.paymentStatus === "Expired" ? "Cancelled · payment expired" : "Cancelled";
  }
  return order.paymentMethod === "Online" ? "Completed · paid online" : "Completed · paid at counter";
}

export function OrderHistory() {
  const [status, setStatus] = useState<HistoryStatus>("Completed");
  const [draft, setDraft] = useState("");
  const [search, setSearch] = useState("");
  const [result, setResult] = useState<Result | null>(null);
  const key = `${status}|${search}`;

  useEffect(() => {
    const controller = new AbortController();
    const requestKey = `${status}|${search}`;
    api
      .getAdminOrders({ status, search }, controller.signal)
      .then((orders) => setResult({ key: requestKey, orders }))
      .catch(() => {
        if (!controller.signal.aborted) setResult({ key: requestKey, error: true });
      });
    return () => controller.abort();
  }, [status, search]);

  const current = result?.key === key ? result : null; // null = still loading this combination

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSearch(draft.trim());
  }

  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-semibold tracking-tight">Order history</h1>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div role="group" aria-label="Order status" className="flex gap-2">
          {(["Completed", "Cancelled"] as const).map((s) => (
            <Button
              key={s}
              size="sm"
              variant={status === s ? "default" : "outline"}
              aria-pressed={status === s}
              onClick={() => setStatus(s)}
            >
              {s}
            </Button>
          ))}
        </div>

        <form onSubmit={submit} className="flex items-center gap-2" role="search">
          <Input
            type="search"
            aria-label="Search order history"
            placeholder="Name, phone, email, or order code"
            className="w-72"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
          />
          <Button type="submit" variant="outline">Search</Button>
        </form>
      </div>

      {current === null ? (
        <div className="space-y-3" aria-busy="true">
          {[0, 1, 2].map((i) => <div key={i} className="h-24 animate-pulse rounded-xl bg-muted" />)}
        </div>
      ) : "error" in current ? (
        <p role="alert" className="py-12 text-center">We couldn&apos;t load the history. Please try again.</p>
      ) : current.orders.length === 0 ? (
        <p className="py-12 text-center text-muted-foreground">No orders found.</p>
      ) : (
        <ul className="space-y-3">
          {current.orders.map((order) => (
            <li
              key={order.id}
              data-testid="history-order"
              className="flex flex-wrap items-center justify-between gap-3 rounded-xl border p-4"
            >
              <div className="space-y-1">
                <p className="font-medium">
                  #{order.id.slice(0, 8).toUpperCase()} · {order.customerName}
                </p>
                <p className="text-sm text-muted-foreground">
                  {order.items.map((i) => `${i.quantity} × ${i.productName}`).join(", ")}
                </p>
                <p className="text-sm text-muted-foreground">
                  Pickup {formatPhtShort(order.pickupTime)} · Updated {formatPhtShort(order.updatedAt)}
                </p>
              </div>
              <div className="flex items-center gap-3">
                <div className="text-right">
                  <p className="font-semibold">{formatPeso(order.totalAmount)}</p>
                  <Badge variant={order.status === "Cancelled" ? "destructive" : "secondary"}>
                    {describe(order)}
                  </Badge>
                </div>
                <Button asChild size="sm" variant="ghost">
                  <Link href={`/orders/${order.id}`}>Details</Link>
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}