"use client";

import Link from "next/link";
import { ShoppingBag, Trash2 } from "lucide-react";
import { QuantityStepper } from "@/components/quantity-stepper";
import { Button } from "@/components/ui/button";
import { formatPeso } from "@/lib/format";
import { useHydrated } from "@/lib/use-hydrated";
import { selectSubtotal, useCartStore } from "@/stores/cart-store";

export function CartView() {
  const hydrated = useHydrated();
  const items = useCartStore((s) => s.items);
  const subtotal = useCartStore(selectSubtotal);
  const setQuantity = useCartStore((s) => s.setQuantity);
  const remove = useCartStore((s) => s.remove);
  const clear = useCartStore((s) => s.clear);

  if (!hydrated) {
    return <div className="h-48 animate-pulse rounded-xl bg-muted" aria-busy="true" />;
  }

  if (items.length === 0) {
    return (
      <div className="space-y-4 py-16 text-center">
        <ShoppingBag className="mx-auto size-10 text-muted-foreground" aria-hidden />
        <h2 className="text-xl font-medium">Your cart is empty</h2>
        <p className="text-muted-foreground">Add something delicious from the menu.</p>
        <Button asChild>
          <Link href="/">Browse the menu</Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="grid gap-8 lg:grid-cols-[1fr_20rem]">
      <ul className="divide-y rounded-xl border" aria-label="Items in your cart">
        {items.map((item) => (
          <li
            key={item.variantId}
            data-testid="cart-line"
            className="flex flex-wrap items-center justify-between gap-4 p-4"
          >
            <div>
              <p className="font-medium">{item.productName}</p>
              <p className="text-sm text-muted-foreground">
                {item.variantName} · {formatPeso(item.unitPrice)}
              </p>
            </div>
            <div className="flex items-center gap-4">
              <QuantityStepper
                value={item.quantity}
                label={`${item.productName} (${item.variantName})`}
                onChange={(q) => setQuantity(item.variantId, q)}
              />
              <p className="w-20 text-right font-medium">{formatPeso(item.unitPrice * item.quantity)}</p>
              <Button
                variant="ghost"
                size="icon"
                aria-label={`Remove ${item.productName} (${item.variantName})`}
                onClick={() => remove(item.variantId)}
              >
                <Trash2 aria-hidden />
              </Button>
            </div>
          </li>
        ))}
      </ul>

      <aside className="h-fit space-y-4 rounded-xl border p-4">
        <h2 className="text-lg font-medium">Summary</h2>
        <div className="flex items-center justify-between">
          <span className="text-muted-foreground">Subtotal</span>
          <span className="text-lg font-semibold" data-testid="cart-subtotal">{formatPeso(subtotal)}</span>
        </div>
        <p className="text-sm text-muted-foreground">
          Prices are confirmed when you place your order. VAT is included.
        </p>
        <Button asChild className="w-full">
          <Link href="/checkout">Checkout</Link>
        </Button>
        <Button variant="ghost" className="w-full" onClick={clear}>
          Clear cart
        </Button>
      </aside>
    </div>
  );
}