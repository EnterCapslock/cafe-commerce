import { Coffee, Croissant, ShoppingBag, type LucideIcon } from "lucide-react";
import type { ProductCategory } from "@/types/api";

const icons: Record<ProductCategory, LucideIcon> = {
  Coffee,
  Food: Croissant,
  Merchandise: ShoppingBag,
};

export function CategoryIcon({ category, className }: { category: ProductCategory; className?: string }) {
  const Icon = icons[category];
  return <Icon className={className} aria-hidden />;
}