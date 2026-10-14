"use client";

import { Plus } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";
import { DeleteProductDialog } from "@/components/admin/delete-product-dialog";
import { CategoryIcon } from "@/components/menu/category-icon";
import { NativeSelect } from "@/components/shared/native-select";
import { ProductImage } from "@/components/shared/product-image";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { api } from "@/lib/api/client";
import { describeApiError } from "@/lib/api/errors";
import { priceRange, toInput } from "@/lib/admin-products";
import { useAdminProducts } from "@/lib/hooks/use-admin-products";
import { cn } from "@/lib/utils";
import type { AdminProduct, ProductCategory, ProductInput } from "@/types/api";

type Visibility = "active" | "archived" | "all";

export function MenuManager() {
  const { state, reload, upsert, remove } = useAdminProducts();
  const [category, setCategory] = useState<ProductCategory | "All">("All");
  const [visibility, setVisibility] = useState<Visibility>("active");
  const [query, setQuery] = useState("");
  const [busy, setBusy] = useState<ReadonlySet<number>>(() => new Set());
  const [deleting, setDeleting] = useState<AdminProduct | null>(null);

  async function save(product: AdminProduct, input: ProductInput, success?: string) {
    setBusy((prev) => new Set(prev).add(product.id));
    try {
      upsert(await api.updateProduct(product.id, input));
      if (success) toast.success(success);
    } catch (error) {
      toast.error(describeApiError(error)[0]);
    } finally {
      setBusy((prev) => {
        const next = new Set(prev);
        next.delete(product.id);
        return next;
      });
    }
  }

  if (state.status === "loading") {
    return (
      <div className="space-y-4" aria-busy="true">
        <h1 className="text-3xl font-semibold tracking-tight">Menu</h1>
        {[0, 1, 2, 3].map((i) => <div key={i} className="h-16 animate-pulse rounded-xl bg-muted" />)}
      </div>
    );
  }

  if (state.status === "unauthorized") {
    return (
      <div className="mx-auto max-w-md space-y-4 py-16 text-center">
        <h1 className="text-2xl font-semibold">Your session has ended</h1>
        <Button asChild><Link href="/login?next=/admin/menu">Sign in</Link></Button>
      </div>
    );
  }

  if (state.status === "error") {
    return (
      <div className="mx-auto max-w-md space-y-4 py-16 text-center">
        <h1 className="text-2xl font-semibold">We couldn&apos;t load the menu</h1>
        <p className="text-muted-foreground">The server may be waking up.</p>
        <Button variant="outline" onClick={reload}>Try again</Button>
      </div>
    );
  }

  const { products } = state;
  const archivedCount = products.filter((p) => !p.isActive).length;
  const needle = query.trim().toLowerCase();

  const visible = products.filter(
    (p) =>
      (visibility === "all" || (visibility === "active" ? p.isActive : !p.isActive)) &&
      (category === "All" || p.category === category) &&
      (!needle || p.name.toLowerCase().includes(needle)),
  );

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-3xl font-semibold tracking-tight">Menu</h1>
        <Button asChild>
          <Link href="/admin/menu/new"><Plus aria-hidden /> New item</Link>
        </Button>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <Input
          type="search" aria-label="Search items" placeholder="Search by name" className="w-56"
          value={query} onChange={(e) => setQuery(e.target.value)}
        />
        <NativeSelect
          aria-label="Filter by category" className="w-40"
          value={category} onChange={(e) => setCategory(e.target.value as ProductCategory | "All")}
        >
          <option value="All">All categories</option>
          <option value="Coffee">Coffee</option>
          <option value="Food">Food</option>
          <option value="Merchandise">Merchandise</option>
        </NativeSelect>
        <NativeSelect
          aria-label="Filter by visibility" className="w-44"
          value={visibility} onChange={(e) => setVisibility(e.target.value as Visibility)}
        >
          <option value="active">On the menu</option>
          <option value="archived">Archived ({archivedCount})</option>
          <option value="all">All items</option>
        </NativeSelect>
      </div>

      {visible.length === 0 ? (
        <p className="rounded-xl border border-dashed p-10 text-center text-muted-foreground">
          No items match.
        </p>
      ) : (
        <div className="overflow-x-auto rounded-xl border">
          <Table>
            <TableCaption className="sr-only">Menu items</TableCaption>
            <TableHeader>
              <TableRow>
                <TableHead>Item</TableHead>
                <TableHead>Category</TableHead>
                <TableHead>Price</TableHead>
                <TableHead>Options (click to mark sold out)</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {visible.map((p) => {
                const isBusy = busy.has(p.id);
                return (
                  <TableRow key={p.id} data-testid="menu-row">
                    <TableCell>
                      <div className="flex items-center gap-3">
                        <div className="relative flex size-12 shrink-0 items-center justify-center overflow-hidden rounded-md bg-muted text-muted-foreground">
                          {p.imageUrl ? (
                            <ProductImage url={p.imageUrl} sizes="48px" />
                          ) : (
                            <CategoryIcon category={p.category} className="size-5" />
                          )}
                        </div>
                        <div className="min-w-0">
                          <p className="font-medium">{p.name}</p>
                          <p className="line-clamp-1 max-w-xs text-xs text-muted-foreground">{p.description}</p>
                        </div>
                      </div>
                    </TableCell>
                    <TableCell>{p.category}</TableCell>
                    <TableCell className="whitespace-nowrap">{priceRange(p)}</TableCell>
                    <TableCell>
                      <div className="flex max-w-xs flex-wrap gap-1.5">
                        {[...p.variants].sort((a, b) => a.sortOrder - b.sortOrder).map((v) => (
                          <button
                            key={v.id}
                            type="button"
                            aria-pressed={v.isAvailable}
                            aria-label={`${p.name} ${v.name} available`}
                            disabled={isBusy}
                            onClick={() =>
                              void save(p, toInput(p, { availability: { variantId: v.id, isAvailable: !v.isAvailable } }))
                            }
                            className={cn(
                              "rounded-full border px-2.5 py-0.5 text-xs transition-colors disabled:opacity-50",
                              v.isAvailable
                                ? "border-primary/40 bg-primary/10"
                                : "text-muted-foreground line-through",
                            )}
                          >
                            {v.name}
                          </button>
                        ))}
                      </div>
                    </TableCell>
                    <TableCell>
                      <Badge variant={p.isActive ? "secondary" : "outline"}>{p.isActive ? "On menu" : "Archived"}</Badge>
                    </TableCell>
                    <TableCell>
                      <div className="flex justify-end gap-1">
                        <Button asChild size="sm" variant="outline">
                          <Link href={`/admin/menu/${p.id}`} aria-label={`Edit ${p.name}`}>Edit</Link>
                        </Button>
                        <Button
                          size="sm" variant="ghost" disabled={isBusy}
                          aria-label={`${p.isActive ? "Archive" : "Restore"} ${p.name}`}
                          onClick={() =>
                            void save(
                              p,
                              toInput(p, { isActive: !p.isActive }),
                              p.isActive ? `${p.name} archived` : `${p.name} is back on the menu`,
                            )
                          }
                        >
                          {p.isActive ? "Archive" : "Restore"}
                        </Button>
                        <Button
                          size="sm" variant="ghost" className="text-destructive" disabled={isBusy}
                          aria-label={`Delete ${p.name}`} onClick={() => setDeleting(p)}
                        >
                          Delete
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
      )}

      <DeleteProductDialog
        key={deleting?.id ?? 0}
        product={deleting}
        onClose={() => setDeleting(null)}
        onDeleted={(p) => remove(p.id)}
        onArchive={(p) => save(p, toInput(p, { isActive: false }), `${p.name} archived`)}
      />
    </div>
  );
}