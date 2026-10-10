import { CategoryFilter } from "@/components/menu/category-filter";
import { ProductCard } from "@/components/menu/product-card";
import { api } from "@/lib/api";
import type { ProductCategory } from "@/types/api";

const CATEGORIES: readonly ProductCategory[] = ["Coffee", "Food", "Merchandise"];

function parseCategory(value: string | string[] | undefined): ProductCategory | undefined {
  const first = Array.isArray(value) ? value[0] : value;
  return CATEGORIES.find((c) => c === first);   // anything else falls back to "All"
}

export default async function MenuPage({
  searchParams,
}: {
  searchParams: Promise<{ category?: string | string[] }>;
}) {
  const category = parseCategory((await searchParams).category);
  const products = await api.getMenu(category);

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h1 className="text-3xl font-semibold tracking-tight">Our menu</h1>
        <p className="text-muted-foreground">Order ahead and pick up in store. Pay online or at the counter.</p>
      </div>

      <CategoryFilter active={category} />

      {products.length === 0 ? (
        <p className="py-12 text-center text-muted-foreground">Nothing here yet. Check back soon.</p>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {products.map((p) => <ProductCard key={p.id} product={p} />)}
        </div>
      )}
    </div>
  );
}