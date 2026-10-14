"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from "@/components/ui/dialog";
import { QuantityStepper } from "@/components/shared/quantity-stepper";
import { api, ApiError } from "@/lib/api/client";
import { formatPeso } from "@/lib/format";
import { cn } from "@/lib/utils";
import { useCartStore } from "@/stores/cart-store";
import type { ProductDetail, ProductSummary } from "@/types/api";

interface Props {
  product: ProductSummary;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

type LoadState =
  | { status: "loading" }
  | { status: "error"; message: string }
  | { status: "ready"; detail: ProductDetail };

export function ProductDialog({ product, open, onOpenChange }: Props) {
  const [attempt, setAttempt] = useState(0); // changing the key remounts the body and refetches

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{product.name}</DialogTitle>
          <DialogDescription>{product.description}</DialogDescription>
        </DialogHeader>
        <DialogBody
          key={attempt}
          product={product}
          onRetry={() => setAttempt((a) => a + 1)}
          onAdded={() => onOpenChange(false)}
        />
      </DialogContent>
    </Dialog>
  );
}

// Mounted only while the dialog is open, so it loads fresh variants (and availability) every time.
function DialogBody({ product, onRetry, onAdded }: {
  product: ProductSummary;
  onRetry: () => void;
  onAdded: () => void;
}) {
  const [state, setState] = useState<LoadState>({ status: "loading" });

  useEffect(() => {
    let cancelled = false;
    api.getProduct(product.id)
      .then((detail) => { if (!cancelled) setState({ status: "ready", detail }); })
      .catch((error: unknown) => {
        if (cancelled) return;
        const message = error instanceof ApiError && error.status === 404
          ? "This item is no longer on the menu."
          : "We couldn't load the options.";
        setState({ status: "error", message });
      });
    return () => { cancelled = true; };
  }, [product.id]);

  if (state.status === "loading") {
    return (
      <div className="space-y-2" aria-busy="true" aria-label="Loading options">
        {[0, 1, 2].map((i) => <div key={i} className="h-12 animate-pulse rounded-lg bg-muted" />)}
      </div>
    );
  }

  if (state.status === "error") {
    return (
      <div className="space-y-3 text-sm">
        <p role="alert">{state.message}</p>
        <Button variant="outline" onClick={onRetry}>Try again</Button>
      </div>
    );
  }

  return <VariantPicker detail={state.detail} onAdded={onAdded} />;
}

function VariantPicker({ detail, onAdded }: { detail: ProductDetail; onAdded: () => void }) {
  const add = useCartStore((s) => s.add);
  const [variantId, setVariantId] = useState<number | null>(
    () => detail.variants.find((v) => v.isAvailable)?.id ?? null,
  );
  const [quantity, setQuantity] = useState(1);
  const selected = detail.variants.find((v) => v.id === variantId);

  function handleAdd() {
    if (!selected) return;
    add(
      {
        variantId: selected.id,
        productId: detail.id,
        productName: detail.name,
        variantName: selected.name,
        unitPrice: selected.price,
      },
      quantity,
    );
    toast.success(`${quantity} × ${detail.name} (${selected.name}) added to your order`);
    onAdded();
  }

  return (
    <>
      <div role="radiogroup" aria-label={`Options for ${detail.name}`} className="grid gap-2">
        {detail.variants.map((v) => (
          <button
            key={v.id}
            type="button"
            role="radio"
            aria-checked={v.id === variantId}
            disabled={!v.isAvailable}
            onClick={() => setVariantId(v.id)}
            className={cn(
              "flex items-center justify-between rounded-lg border px-4 py-3 text-left text-sm transition-colors",
              "disabled:cursor-not-allowed disabled:opacity-50",
              v.id === variantId ? "border-primary bg-primary/5 ring-1 ring-primary" : "hover:bg-muted",
            )}
          >
            <span className="font-medium">{v.name}</span>
            <span>{v.isAvailable ? formatPeso(v.price) : "Sold out"}</span>
          </button>
        ))}
      </div>

      <div className="flex items-center justify-between">
        <span className="text-sm text-muted-foreground">Quantity</span>
        <QuantityStepper value={quantity} onChange={setQuantity} label={detail.name} />
      </div>

      <DialogFooter>
        <Button className="w-full" disabled={!selected} onClick={handleAdd}>
          {selected ? `Add to order · ${formatPeso(selected.price * quantity)}` : "Unavailable"}
        </Button>
      </DialogFooter>
    </>
  );
}