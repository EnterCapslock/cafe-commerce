import Link from "next/link";  // (server component: plain links, so filters are shareable URLs)
import { cn } from "@/lib/utils";
import type { ProductCategory } from "@/types/api";

const FILTERS: { label: string; value?: ProductCategory }[] = [
  { label: "All" },
  { label: "Coffee", value: "Coffee" },
  { label: "Food", value: "Food" },
  { label: "Merchandise", value: "Merchandise" },
];

export function CategoryFilter({ active }: { active?: ProductCategory }) {
  return (
    <nav aria-label="Menu categories" className="flex flex-wrap gap-2">
      {FILTERS.map((f) => {
        const isActive = f.value === active;
        return (
          <Link
            key={f.label}
            href={f.value ? `/?category=${f.value}` : "/"}
            aria-current={isActive ? "page" : undefined}
            className={cn(
              "rounded-full border px-4 py-1.5 text-sm font-medium transition-colors",
              isActive ? "border-primary bg-primary text-primary-foreground" : "hover:bg-muted",
            )}
          >
            {f.label}
          </Link>
        );
      })}
    </nav>
  );
}