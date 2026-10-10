"use client";

import Link from "next/link";
import { CircleAlert, CircleCheck, CircleX, CreditCard } from "lucide-react";
import { useEffect, useState } from "react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { api, ApiError } from "@/lib/api";
import { formatPeso } from "@/lib/format";
import { formatPht } from "@/lib/time";
import { cn } from "@/lib/utils";
import type { OrderDto, OrderStatus } from "@/types/api";

type State =
  | { status: "loading" }
  | { status: "notfound" }
  | { status: "error" }
  | { status: "ready"; order: OrderDto; stale: boolean };

const STATUS_LABEL: Record<OrderStatus, string> = {
  Pending: "Order received",
  Preparing: "Being prepared",
  ReadyForPickup: "Ready for pickup",
  Completed: "Completed",
  Cancelled: "Cancelled",
};

const PROGRESS: OrderStatus[] = ["Pending", "Preparing", "ReadyForPickup", "Completed"];

/** How long to wait before checking again, or null to stop. */
function pollDelay(order: OrderDto | null): number | null {
  if (!order) return 5000;
  if (order.status === "Completed" || order.status === "Cancelled") return null;
  if (order.paymentMethod === "Online" && order.paymentStatus === "Unpaid") return 3000; // waiting for the webhook
  return 10000;
}

export function OrderTracker({ id }: { id: string }) {
  const [state, setState] = useState<State>({ status: "loading" });

  useEffect(() => {
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout> | undefined;
    let latest: OrderDto | null = null;

    const run = async () => {
      try {
        latest = await api.getOrder(id, controller.signal);
        setState({ status: "ready", order: latest, stale: false });
      } catch (error) {
        if (controller.signal.aborted) return;
        if (error instanceof ApiError && error.status === 404) {
          setState({ status: "notfound" });
          return;
        }
        setState((prev) => (prev.status === "ready" ? { ...prev, stale: true } : { status: "error" }));
      }
      const delay = pollDelay(latest);
      if (delay !== null) timer = setTimeout(run, delay);
    };

    timer = setTimeout(run, 0);
    return () => {
      controller.abort();
      clearTimeout(timer);
    };
  }, [id]);

  if (state.status === "loading") {
    return (
      <div className="space-y-4" aria-busy="true">
        <p role="status" className="text-muted-foreground">Loading your order…</p>
        <div className="h-64 animate-pulse rounded-xl bg-muted" />
      </div>
    );
  }

  if (state.status === "notfound") {
    return (
      <div className="space-y-4 py-16 text-center">
        <h1 className="text-2xl font-semibold">We couldn&apos;t find that order</h1>
        <p className="text-muted-foreground">Check the link, or place a new order.</p>
        <Button asChild><Link href="/">Back to the menu</Link></Button>
      </div>
    );
  }

  if (state.status === "error") {
    return (
      <div className="space-y-2 py-16 text-center">
        <h1 className="text-2xl font-semibold">We couldn&apos;t load your order</h1>
        <p role="status" className="text-muted-foreground">
          The server may be waking up. We&apos;ll keep trying automatically.
        </p>
      </div>
    );
  }

  const { order, stale } = state;
  const awaitingPayment =
    order.paymentMethod === "Online" && order.paymentStatus === "Unpaid" && order.status !== "Cancelled";
  const currentStep = PROGRESS.indexOf(order.status);

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-3xl font-semibold tracking-tight">
            Order <span data-testid="order-code">{order.id.slice(0, 8).toUpperCase()}</span>
          </h1>
          <p className="text-sm text-muted-foreground">Keep this page&apos;s link to check on your order later.</p>
        </div>
        <Badge variant={order.status === "Cancelled" ? "destructive" : "secondary"} data-testid="order-status">
          {awaitingPayment ? "Awaiting payment" : STATUS_LABEL[order.status]}
        </Badge>
      </div>

      {stale && <p role="status" className="text-sm text-muted-foreground">Reconnecting…</p>}

      <PaymentNotice order={order} />

      {order.status !== "Cancelled" && (
        <ol className="grid grid-cols-4 gap-2 text-center text-xs" aria-label="Order progress">
          {PROGRESS.map((s, i) => (
            <li key={s} className="space-y-1">
              <div className={cn("h-1.5 rounded-full", i <= currentStep && !awaitingPayment ? "bg-primary" : "bg-muted")} />
              <span className={cn(i === currentStep && !awaitingPayment ? "font-medium" : "text-muted-foreground")}>
                {s === "Pending" && awaitingPayment ? "Awaiting payment" : STATUS_LABEL[s]}
              </span>
            </li>
          ))}
        </ol>
      )}

      <section className="space-y-3 rounded-xl border p-4" aria-label="Order details">
        <dl className="space-y-2 text-sm">
          <div className="flex justify-between gap-4">
            <dt className="text-muted-foreground">Pickup</dt>
            <dd className="font-medium">{formatPht(order.pickupTime)}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-muted-foreground">Name</dt>
            <dd>{order.customerName}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-muted-foreground">Payment</dt>
            <dd>{order.paymentMethod === "Online" ? "Online" : "At the counter"}</dd>
          </div>
        </dl>
        <hr />
        <ul className="space-y-2 text-sm">
          {order.items.map((i) => (
            <li key={`${i.variantId}`} className="flex justify-between gap-4">
              <span>
                {i.quantity} × {i.productName} <span className="text-muted-foreground">({i.variantName})</span>
              </span>
              <span>{formatPeso(i.lineTotal)}</span>
            </li>
          ))}
        </ul>
        <hr />
        <div className="flex justify-between font-semibold">
          <span>Total</span>
          <span data-testid="order-total">{formatPeso(order.totalAmount)}</span>
        </div>
      </section>

      <Button asChild variant="outline"><Link href="/">Back to the menu</Link></Button>
    </div>
  );
}

function PaymentNotice({ order }: { order: OrderDto }) {
  if (order.status === "Cancelled") {
    return (
      <Alert variant="destructive">
        <CircleX aria-hidden />
        <AlertTitle>Order cancelled</AlertTitle>
        <AlertDescription>
          {order.paymentStatus === "Expired"
            ? "Payment wasn't completed in time, so this order was cancelled. No payment was received."
            : order.paymentStatus === "Paid"
              ? "This order was cancelled after payment. Please contact the cafe about a refund."
              : "This order was cancelled. Please contact the cafe if you have questions."}
        </AlertDescription>
      </Alert>
    );
  }

  if (order.paymentMethod === "Online" && order.paymentStatus === "Unpaid") {
    return (
      <Alert>
        <CreditCard aria-hidden />
        <AlertTitle>Complete your payment</AlertTitle>
        <AlertDescription>
          <p>Your order is held for a short time while we wait for your payment.</p>
          {order.checkoutUrl && (
            <Button asChild className="mt-3">
              <a href={order.checkoutUrl}>Pay {formatPeso(order.totalAmount)} now</a>
            </Button>
          )}
          <p className="mt-3">Already paid? This page updates automatically.</p>
        </AlertDescription>
      </Alert>
    );
  }

  if (order.paymentStatus === "Paid") {
    return (
      <Alert>
        <CircleCheck aria-hidden />
        <AlertTitle>Payment received</AlertTitle>
        <AlertDescription>
          {order.paidAt ? `Paid ${formatPht(order.paidAt)}. ` : ""}Thank you!
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <Alert>
      <CircleAlert aria-hidden />
      <AlertTitle>Pay at the counter</AlertTitle>
      <AlertDescription>
        Please pay {formatPeso(order.totalAmount)} when you pick up your order.
      </AlertDescription>
    </Alert>
  );
}