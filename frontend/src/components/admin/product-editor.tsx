"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { ProductForm } from "@/components/admin/product-form";
import { ProductImageUploader } from "@/components/admin/product-image-uploader";
import { Button } from "@/components/ui/button";
import { api, ApiError } from "@/lib/api/client";
import type { AdminProduct } from "@/types/api";

type State =
  | { status: "loading" }
  | { status: "notfound" }
  | { status: "error" }
  | { status: "ready"; product: AdminProduct };

export function ProductEditor({ id }: { id: number }) {
  const [state, setState] = useState<State>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    api
      .getAdminProduct(id, controller.signal)
      .then((product) => setState({ status: "ready", product }))
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        setState({ status: error instanceof ApiError && error.status === 404 ? "notfound" : "error" });
      });
    return () => controller.abort();
  }, [id, attempt]);

  if (state.status === "loading") {
    return <div className="h-64 animate-pulse rounded-xl bg-muted" aria-busy="true" />;
  }

  if (state.status === "notfound") {
    return (
      <div className="space-y-4 py-12 text-center">
        <h1 className="text-2xl font-semibold">That item doesn&apos;t exist</h1>
        <Button asChild><Link href="/admin/menu">Back to the menu</Link></Button>
      </div>
    );
  }

  if (state.status === "error") {
    return (
      <div className="space-y-4 py-12 text-center">
        <p role="alert">We couldn&apos;t load this item.</p>
        <Button
          variant="outline"
          onClick={() => {
            setState({ status: "loading" });
            setAttempt((a) => a + 1);
          }}
        >
          Try again
        </Button>
      </div>
    );
  }

  const { product } = state;
  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-semibold tracking-tight">Edit {product.name}</h1>
      <div className="grid items-start gap-8 lg:grid-cols-[1fr_18rem]">
        <ProductForm product={product} />
        <ProductImageUploader productId={product.id} initialUrl={product.imageUrl} />
      </div>
    </div>
  );
}