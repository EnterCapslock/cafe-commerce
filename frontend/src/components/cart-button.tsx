"use client";

import Link from "next/link";
import { ShoppingCart } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useHydrated } from "@/lib/use-hydrated";
import { selectCount, useCartStore } from "@/stores/cart-store";

export function CartButton() {
  const stored = useCartStore(selectCount);
  const count = useHydrated() ? stored : 0;

  return (
    <Button asChild variant="outline" size="sm">
      <Link href="/cart" aria-label={`Cart, ${count} ${count === 1 ? "item" : "items"}`}>
        <ShoppingCart aria-hidden />
        <span>Cart</span>
        {count > 0 && (
          <span data-testid="cart-count"
            className="rounded-full bg-primary px-2 text-xs font-medium text-primary-foreground">
            {count}
          </span>
        )}
      </Link>
    </Button>
  );
}