import { useEffect, useState } from "react";
import { checkCart, type VariantCheck } from "@/lib/cart-check";

/** What the server currently says about each cart line, or null while checking (or if it can't be reached). */
export function useCartCheck(variantIds: number[]): ReadonlyMap<number, VariantCheck> | null {
  const key = [...variantIds].sort((a, b) => a - b).join(",");
  const [result, setResult] = useState<{ key: string; byId: Map<number, VariantCheck> } | null>(null);

  useEffect(() => {
    if (!key) return;
    const controller = new AbortController();
    checkCart(key.split(",").map(Number), controller.signal)
      .then((items) => setResult({ key, byId: new Map(items.map((i) => [i.variantId, i])) }))
      .catch(() => {
        /* offline or the API is asleep: skip. The server checks again when the order is placed. */
      });
    return () => controller.abort();
  }, [key]);

  return result?.key === key ? result.byId : null;
}