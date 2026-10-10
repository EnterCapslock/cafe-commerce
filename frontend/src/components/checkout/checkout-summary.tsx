"use client";

import { formatPeso } from "@/lib/format";
import { selectSubtotal, useCartStore } from "@/stores/cart-store";

export function CheckoutSummary() {
  const items = useCartStore((s) => s.items);
  const subtotal = useCartStore(selectSubtotal);

  return (
    <aside className="h-fit space-y-4 rounded-xl border p-4 lg:sticky lg:top-24">
      <h2 className="text-lg font-medium">Your order</h2>
      <ul className="space-y-2 text-sm">
        {items.map((i) => (
          <li key={i.variantId} className="flex justify-between gap-4">
            <span>
              {i.quantity} × {i.productName} <span className="text-muted-foreground">({i.variantName})</span>
            </span>
            <span>{formatPeso(i.unitPrice * i.quantity)}</span>
          </li>
        ))}
      </ul>
      <hr />
      <div className="flex items-center justify-between font-semibold">
        <span>Total</span>
        <span>{formatPeso(subtotal)}</span>
      </div>
      <p className="text-xs text-muted-foreground">Prices are confirmed when you place your order.</p>
    </aside>
  );
}