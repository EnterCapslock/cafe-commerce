import type { Metadata } from "next";

export const metadata: Metadata = { title: "Menu" };

export default function AdminMenuPage() {
  return (
    <div className="space-y-2">
      <h1 className="text-3xl font-semibold tracking-tight">Menu</h1>
      <p className="text-muted-foreground">Menu and inventory management arrives in the next step.</p>
    </div>
  );
}