import type { Metadata } from "next";
import { ProductForm } from "@/components/admin/product-form";

export const metadata: Metadata = { title: "New item" };

export default function NewProductPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-semibold tracking-tight">New menu item</h1>
      <div className="grid items-start gap-8 lg:grid-cols-[1fr_18rem]">
        <ProductForm />
        <aside className="rounded-xl border border-dashed p-4 text-sm text-muted-foreground">
          You can add a photo right after the item is created.
        </aside>
      </div>
    </div>
  );
}