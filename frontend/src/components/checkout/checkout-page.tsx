"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { CheckoutForm } from "@/components/checkout/checkout-form";
import { CheckoutSummary } from "@/components/checkout/checkout-summary";
import { Button } from "@/components/ui/button";
import { useHydrated } from "@/lib/use-hydrated";
import { useCartStore } from "@/stores/cart-store";
import type { OrderDto } from "@/types/api";

export function CheckoutPage() {
  const router = useRouter();
  const hydrated = useHydrated();
  const lineCount = useCartStore((s) => s.items.length);
  const clear = useCartStore((s) => s.clear);
  const [redirecting, setRedirecting] = useState(false);

  // The order exists on the server now, so the cart can go. The `redirecting` flag stops
  // the page from flashing "your cart is empty" while the browser navigates away.
  function handlePlaced(order: OrderDto) {
    setRedirecting(true);
    clear();
    if (order.paymentMethod === "Online" && order.checkoutUrl) {
      window.location.assign(order.checkoutUrl); // PayMongo's hosted page
    } else {
      router.push(`/orders/${order.id}`);
    }
  }

  if (!hydrated) {
    return <div className="h-64 animate-pulse rounded-xl bg-muted" aria-busy="true" />;
  }

  if (redirecting) {
    return (
      <p role="status" className="py-16 text-center text-muted-foreground">
        Order placed! Taking you to the next step…
      </p>
    );
  }

  if (lineCount === 0) {
    return (
      <div className="space-y-4 py-16 text-center">
        <p className="text-muted-foreground">Your cart is empty.</p>
        <Button asChild>
          <Link href="/">Browse the menu</Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="grid gap-8 lg:grid-cols-[1fr_22rem]">
      <CheckoutForm onPlaced={handlePlaced} />
      <CheckoutSummary />
    </div>
  );
}