"use client";

import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { CategoryIcon } from "@/components/menu/category-icon";
import { ProductDialog } from "@/components/menu/product-dialog";
import { formatPeso } from "@/lib/format";
import type { ProductSummary } from "@/types/api";

export function ProductCard({ product }: { product: ProductSummary }) {
  const [open, setOpen] = useState(false);
  const soldOut = !product.isAvailable;
  const hasOptions = product.variantCount > 1;

  return (
    <>
      <Card className="flex h-full flex-col overflow-hidden" data-testid="product-card">
        {/* Product photos arrive with image upload in Milestone 6. */}
        <div className="flex h-32 items-center justify-center bg-muted text-muted-foreground">
          <CategoryIcon category={product.category} className="size-10" />
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
        <CardFooter>
          <Button className="w-full" disabled={soldOut} onClick={() => setOpen(true)}>
            {hasOptions ? "Choose options" : "Add to order"}
          </Button>
        </CardFooter>
      </Card>
      <ProductDialog product={product} open={open} onOpenChange={setOpen} />
    </>
  );
}