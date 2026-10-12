"use client";

import { Phone } from "lucide-react";
import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { formatPeso } from "@/lib/format";
import { formatPhtShort, minutesUntil } from "@/lib/time";
import { cn } from "@/lib/utils";
import type { OrderDto, OrderStatus } from "@/types/api";

interface Step {
  target: OrderStatus;
  label: string;
}

/** The one forward move for an order, or null if it has none. */
export function nextStep(order: OrderDto): Step | null {
  switch (order.status) {
    case "Pending":
      return { target: "Preparing", label: "Start preparing" };
    case "Preparing":
      return { target: "ReadyForPickup", label: "Mark ready" };
    case "ReadyForPickup":
      return {
        target: "Completed",
        label: order.paymentMethod === "PayAtCounter" ? `Collect ${formatPeso(order.totalAmount)} & complete` : "Complete",
      };
    default:
      return null;
  }
}

function relativePickup(minutes: number): { text: string; late: boolean } {
  if (minutes === 0) return { text: "now", late: false };
  const abs = Math.abs(minutes);
  const amount = abs >= 60 ? `${Math.round(abs / 60)} h` : `${abs} min`;
  return minutes > 0 ? { text: `in ${amount}`, late: false } : { text: `${amount} overdue`, late: true };
}

interface Props {
  order: OrderDto;
  now: number;
  isNew: boolean;
  busy: boolean;
  onAdvance: (order: OrderDto) => void;
  onCancel: (order: OrderDto) => void;
}

export function OrderCard({ order, now, isNew, busy, onAdvance, onCancel }: Props) {
  const code = order.id.slice(0, 8).toUpperCase();
  const step = nextStep(order);
  const minutes = minutesUntil(order.pickupTime, now);
  const relative = now > 0 && Math.abs(minutes) < 24 * 60 ? relativePickup(minutes) : null;

  return (
    <li
      data-testid="board-order"
      className={cn("space-y-3 rounded-xl border bg-card p-4", isNew && "ring-2 ring-primary")}
    >
      <div className="flex items-start justify-between gap-2">
        <div>
          <p className="font-semibold tracking-wide">#{code}</p>
          <p className="text-sm">{order.customerName}</p>
        </div>
        <div className="flex flex-wrap justify-end gap-1">
          {isNew && <Badge>New</Badge>}
          {order.paymentMethod === "Online" ? (
            <Badge variant="secondary">Paid online</Badge>
          ) : (
            <Badge variant="outline">Pay at counter</Badge>
          )}
        </div>
      </div>

      <p className="text-sm">
        <span className="font-medium">Pickup {formatPhtShort(order.pickupTime)}</span>
        {relative && (
          <span className={cn(relative.late ? "font-medium text-destructive" : "text-muted-foreground")}>
            {" · "}
            {relative.text}
          </span>
        )}
      </p>

      <ul className="space-y-1 text-sm">
        {order.items.map((item) => (
          <li key={item.variantId}>
            {item.quantity} × {item.productName} <span className="text-muted-foreground">({item.variantName})</span>
          </li>
        ))}
      </ul>

      <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
        <span className="font-semibold">{formatPeso(order.totalAmount)}</span>
        <a
          href={`tel:${order.customerPhone}`}
          className="inline-flex items-center gap-1 text-muted-foreground underline-offset-4 hover:underline"
        >
          <Phone className="size-3.5" aria-hidden />
          {order.customerPhone}
        </a>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        {step && (
          <Button
            size="sm"
            disabled={busy}
            aria-label={`${step.label}, order ${code}`}
            onClick={() => onAdvance(order)}
          >
            {step.label}
          </Button>
        )}
        <Button
          size="sm"
          variant="ghost"
          disabled={busy}
          aria-label={`Cancel order ${code}`}
          onClick={() => onCancel(order)}
        >
          Cancel
        </Button>
        <Button asChild size="sm" variant="ghost" className="ml-auto">
          <Link href={`/orders/${order.id}`} aria-label={`Details for order ${code}`}>
            Details
          </Link>
        </Button>
      </div>
    </li>
  );
}