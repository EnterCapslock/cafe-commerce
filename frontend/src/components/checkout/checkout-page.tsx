"use client";

import { Loader2 } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { useSession } from "@/components/auth/session-provider";
import { CheckoutForm } from "@/components/checkout/checkout-form";
import { CheckoutSummary } from "@/components/checkout/checkout-summary";
import { Button } from "@/components/ui/button";
import { rememberLastOrder, useLastOrderId } from "@/lib/last-order";
import { useHydrated } from "@/lib/use-hydrated";
import { useCartStore } from "@/stores/cart-store";
import type { OrderDto } from "@/types/api";

type RedirectKind = "payment" | "order";

function RedirectScreen({ kind }: { kind: RedirectKind }) {
  return (
    <div
      role="status"
      aria-live="polite"
      className="flex min-h-[50vh] flex-col items-center justify-center gap-4 text-center"
    >
      <Loader2 className="size-10 animate-spin text-muted-foreground motion-reduce:animate-none" aria-hidden />
      <div className="space-y-1">
        <p className="text-lg font-medium">
          {kind === "payment" ? "Taking you to secure payment…" : "Opening your order…"}
        </p>
        <p className="text-sm text-muted-foreground">
          {kind === "payment"
            ? "Your order has been placed. Please don't close this page."
            : "Your order has been placed."}
        </p>
      </div>
    </div>
  );
}

export function CheckoutPage() {
  const router = useRouter();
  const hydrated = useHydrated();
  const { session } = useSession();
  const lineCount = useCartStore((s) => s.items.length);
  const clear = useCartStore((s) => s.clear);
  const lastOrderId = useLastOrderId();
  const [redirect, setRedirect] = useState<RedirectKind | null>(null);
  const placedOrderId = useRef<string | null>(null);

  // Pressing Back from PayMongo can restore this page from the browser's cache with the
  // spinner still showing. Send the customer to their order page, where "Pay now" lives.
  useEffect(() => {
    function onPageShow(event: PageTransitionEvent) {
      if (event.persisted && placedOrderId.current) {
        router.replace(`/orders/${placedOrderId.current}`);
      }
    }
    window.addEventListener("pageshow", onPageShow);
    return () => window.removeEventListener("pageshow", onPageShow);
  }, [router]);

  // The order exists on the server now, so the cart can go. Showing the redirect screen first
  // stops the page from flashing "your cart is empty" while the browser navigates away.
  function handlePlaced(order: OrderDto) {
    const checkoutUrl = order.paymentMethod === "Online" ? order.checkoutUrl : null;
    placedOrderId.current = order.id;
    rememberLastOrder(order.id);
    setRedirect(checkoutUrl ? "payment" : "order");
    clear();

    if (checkoutUrl) {
      window.location.assign(checkoutUrl); // PayMongo's hosted page
    } else {
      router.push(`/orders/${order.id}`);
    }
  }

  // Wait for the session so the form can start with the customer's name and email already filled in.
  if (!hydrated || session.status === "loading") {
    return <div className="h-64 animate-pulse rounded-xl bg-muted" aria-busy="true" />;
  }

  if (redirect) {
    return <RedirectScreen kind={redirect} />;
  }

  if (lineCount === 0) {
    return (
      <div className="space-y-4 py-16 text-center">
        <p className="text-muted-foreground">Your cart is empty.</p>
        {lastOrderId && (
          <p className="text-sm">
            Looking for your order?{" "}
            <Link href={`/orders/${lastOrderId}`} className="font-medium underline underline-offset-4">
              View your last order
            </Link>
          </p>
        )}
        <Button asChild>
          <Link href="/">Browse the menu</Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="grid gap-8 lg:grid-cols-[1fr_22rem]">
      <CheckoutForm
        onPlaced={handlePlaced}
        user={session.status === "authenticated" ? session.user : null}
      />
      <CheckoutSummary />
    </div>
  );
}