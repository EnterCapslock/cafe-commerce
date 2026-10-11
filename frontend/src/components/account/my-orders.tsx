"use client";

import { RefreshCw } from "lucide-react";
import Link from "next/link";
import { useSession } from "@/components/auth/session-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { formatPeso } from "@/lib/format";
import { formatPht } from "@/lib/time";
import { useMyOrders } from "@/lib/use-my-orders";
import type { OrderDto, OrderStatus } from "@/types/api";

const STATUS_LABEL: Record<OrderStatus, string> = {
  Pending: "Order received",
  Preparing: "Being prepared",
  ReadyForPickup: "Ready for pickup",
  Completed: "Completed",
  Cancelled: "Cancelled",
};

function summarize(order: OrderDto): string {
  const shown = order.items.slice(0, 2).map((i) => `${i.quantity} × ${i.productName}`).join(", ");
  const more = order.items.length - 2;
  return more > 0 ? `${shown} +${more} more` : shown;
}

function OrderCard({ order }: { order: OrderDto }) {
  const awaitingPayment =
    order.paymentMethod === "Online" && order.paymentStatus === "Unpaid" && order.status !== "Cancelled";

  return (
    <li className="space-y-3 rounded-xl border p-4" data-testid="my-order">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <p className="font-medium">Order {order.id.slice(0, 8).toUpperCase()}</p>
          <p className="text-sm text-muted-foreground">Placed {formatPht(order.createdAt)}</p>
        </div>
        <Badge variant={order.status === "Cancelled" ? "destructive" : "secondary"}>
          {awaitingPayment ? "Awaiting payment" : STATUS_LABEL[order.status]}
        </Badge>
      </div>

      <p className="text-sm">{summarize(order)}</p>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm">
          <span className="font-semibold">{formatPeso(order.totalAmount)}</span>
          <span className="text-muted-foreground"> · Pickup {formatPht(order.pickupTime)}</span>
        </p>
        <div className="flex gap-2">
          {awaitingPayment && order.checkoutUrl && (
            <Button asChild size="sm">
              <a href={order.checkoutUrl}>Pay now</a>
            </Button>
          )}
          <Button asChild size="sm" variant="outline">
            <Link href={`/orders/${order.id}`}>View order</Link>
          </Button>
        </div>
      </div>
    </li>
  );
}

export function MyOrders() {
  const { session } = useSession();
  const { state, reload } = useMyOrders(session.status === "authenticated");

  if (session.status === "loading") {
    return <div className="h-40 animate-pulse rounded-xl bg-muted" aria-busy="true" />;
  }

  if (session.status === "anonymous") {
    return (
      <div className="space-y-4 py-12 text-center">
        <p className="text-muted-foreground">Sign in to see your orders.</p>
        <Button asChild>
          <Link href="/login?next=/account/orders">Sign in</Link>
        </Button>
      </div>
    );
  }

  if (state.status === "loading") {
    return (
      <div className="space-y-3" aria-busy="true">
        <p role="status" className="text-sm text-muted-foreground">Loading your orders…</p>
        {[0, 1].map((i) => <div key={i} className="h-36 animate-pulse rounded-xl bg-muted" />)}
      </div>
    );
  }

  if (state.status === "error") {
    return (
      <div className="space-y-3 py-12 text-center">
        <p role="alert">We couldn&apos;t load your orders. The server may be waking up.</p>
        <Button variant="outline" onClick={reload}>Try again</Button>
      </div>
    );
  }

  if (state.orders.length === 0) {
    return (
      <div className="space-y-4 py-12 text-center">
        <p className="text-muted-foreground">You haven&apos;t placed any orders yet.</p>
        <Button asChild>
          <Link href="/">Browse the menu</Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">Your {state.orders.length} most recent orders</p>
        <Button variant="ghost" size="sm" onClick={reload}>
          <RefreshCw aria-hidden /> Refresh
        </Button>
      </div>
      <ul className="space-y-3">
        {state.orders.map((o) => <OrderCard key={o.id} order={o} />)}
      </ul>
    </div>
  );
}