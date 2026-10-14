"use client";

import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { CategoryIcon } from "@/components/menu/category-icon";
import { ProductDialog } from "@/components/menu/product-dialog";
import { ProductImage } from "@/components/shared/product-image"
import { formatPeso } from "@/lib/format";
import type { ProductSummary } from "@/types/api";
import { useShopper } from "@/lib/hooks/use-shopper";

export function ProductCard({ product }: { product: ProductSummary }) {
  const [open, setOpen] = useState(false);
  const soldOut = !product.isAvailable;
  const hasOptions = product.variantCount > 1;
  const { isAdmin } = useShopper();

  return (
    <>
      <Card className="flex h-full flex-col overflow-hidden" data-testid="product-card">
        {/* Product photos arrive with image upload in Milestone 6. */}
        <div className="relative flex h-40 items-center justify-center overflow-hidden bg-muted text-muted-foreground">
          {product.imageUrl ? (
            <ProductImage
              url={product.imageUrl}
              sizes="(min-width: 1280px) 25vw, (min-width: 1024px) 33vw, (min-width: 640px) 50vw, 100vw"
            />
          ) : (
            <CategoryIcon category={product.category} className="size-10" />
          )}
        </div>
        <CardHeader>
          <CardTitle>{product.name}</CardTitle>
          <CardDescription className="line-clamp-2">{product.description}</CardDescription>
        </CardHeader>
        <CardContent className="mt-auto flex items-center justify-between">
          <p className="font-medium">
            {hasOptions && <span className="text-sm font-normal text-muted-foreground">From </span>}
            {formatPeso(product.fromPrice)}
          </p>
          {soldOut && <Badge variant="secondary">Sold out</Badge>}
        </CardContent>
        {!isAdmin && (
          <CardFooter>
            <Button className="w-full" disabled={soldOut} onClick={() => setOpen(true)}>
              {hasOptions ? "Choose options" : "Add to order"}
            </Button>
          </CardFooter>
        )}
      </Card>
      <ProductDialog product={product} open={open} onOpenChange={setOpen} />
    </>
  );
}