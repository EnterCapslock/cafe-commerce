import { formatPeso } from "@/lib/format";
import type { AdminProduct, ProductInput } from "@/types/api";

/** Turns a product back into the shape the update endpoint expects, with optional changes. */
export function toInput(
  product: AdminProduct,
  changes: { isActive?: boolean; availability?: { variantId: number; isAvailable: boolean } } = {},
): ProductInput {
  return {
    name: product.name,
    description: product.description,
    category: product.category,
    isActive: changes.isActive ?? product.isActive,
    variants: [...product.variants]
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((v) => ({
        id: v.id,
        name: v.name,
        price: v.price,
        isAvailable: changes.availability?.variantId === v.id ? changes.availability.isAvailable : v.isAvailable,
      })),
  };
}

export function priceRange(product: AdminProduct): string {
  const prices = product.variants.map((v) => v.price);
  if (prices.length === 0) return "—";
  const min = Math.min(...prices);
  const max = Math.max(...prices);
  return min === max ? formatPeso(min) : `${formatPeso(min)} – ${formatPeso(max)}`;
}