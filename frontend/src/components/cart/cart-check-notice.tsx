"use client";

import { TriangleAlert } from "lucide-react";
import { toast } from "sonner";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { formatPeso } from "@/lib/format";
import { useCartCheck } from "@/lib/hooks/use-cart-check";
import { useCartStore } from "@/stores/cart-store";

/** Tells the customer when something in their cart was removed from the menu or changed price. */
export function CartCheckNotice() {
  const items = useCartStore((s) => s.items);
  const remove = useCartStore((s) => s.remove);
  const setPrice = useCartStore((s) => s.setPrice);
  const checked = useCartCheck(items.map((i) => i.variantId));

  if (!checked) return null;

  const unavailable = items.filter((i) => checked.get(i.variantId)?.available === false);
  const repriced = items.filter((i) => {
    const check = checked.get(i.variantId);
    return check?.available && check.price !== null && check.price !== i.unitPrice;
  });
  if (unavailable.length === 0 && repriced.length === 0) return null;

  function update() {
    unavailable.forEach((i) => remove(i.variantId));
    repriced.forEach((i) => {
      const price = checked?.get(i.variantId)?.price;
      if (price != null) setPrice(i.variantId, price);
    });
    toast.success("Your cart is up to date");
  }

  return (
    <Alert data-testid="cart-check-notice">
      <TriangleAlert aria-hidden />
      <AlertTitle>Your cart needs a quick update</AlertTitle>
      <AlertDescription>
        <ul className="list-disc pl-4">
          {unavailable.map((i) => (
            <li key={i.variantId}>
              {i.productName} ({i.variantName}) is no longer available.
            </li>
          ))}
          {repriced.map((i) => (
            <li key={i.variantId}>
              {i.productName} ({i.variantName}) is now {formatPeso(checked.get(i.variantId)?.price ?? i.unitPrice)}{" "}
              (was {formatPeso(i.unitPrice)}).
            </li>
          ))}
        </ul>
        <Button size="sm" className="mt-3" onClick={update}>Update cart</Button>
      </AlertDescription>
    </Alert>
  );
}