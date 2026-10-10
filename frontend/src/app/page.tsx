import { Suspense } from "react";
import { CategoryFilter } from "@/components/menu/category-filter";
import { ProductCard } from "@/components/menu/product-card";
import { api } from "@/lib/api";
import type { ProductCategory } from "@/types/api";

const CATEGORIES: readonly ProductCategory[] = ["Coffee", "Food", "Merchandise"];

function parseCategory(value: string | string[] | undefined): ProductCategory | undefined {
  const first = Array.isArray(value) ? value[0] : value;
  return CATEGORIES.find((c) => c === first);   // anything else falls back to "All"
}

type SearchParams = Promise<{ category?: string | string[] }>;

export default function MenuPage({ searchParams }: { searchParams: SearchParams }) {
  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h1 className="text-3xl font-semibold tracking-tight">Our menu</h1>
        <p className="text-muted-foreground">Order ahead and pick up in store. Pay online or at the counter.</p>
      </div>

      <Suspense
        fallback={
          <div className="space-y-4" aria-busy="true">
            <p role="status" className="text-muted-foreground">
              Brewing the menu… if it&apos;s been a while, our server may take up to a minute to wake up.
            </p>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
              {Array.from({ length: 8 }, (_, i) => (
                <div key={i} className="h-72 animate-pulse rounded-xl bg-muted" />
              ))}
            </div>
          </div>
        }
      >
        <MenuContent searchParams={searchParams} />
      </Suspense>
    </div>
  );
}

async function MenuContent({ searchParams }: { searchParams: SearchParams }) {
  const category = parseCategory((await searchParams).category);
  const products = await api.getMenu(category);

  return (
    <>
      <CategoryFilter active={category} />
      {products.length === 0 ? (
        <p className="py-12 text-center text-muted-foreground">Nothing here yet. Check back soon.</p>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {products.map((p) => <ProductCard key={p.id} product={p} />)}
        </div>
      )}
    </>
  );
}