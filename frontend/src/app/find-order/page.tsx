import type { Metadata } from "next";
import { FindOrderForm } from "@/components/orders/find-order-form";

export const metadata: Metadata = { title: "Find my order" };

export default function FindOrderPage() {
  return (
    <div className="mx-auto w-full max-w-md space-y-6">
      <div className="space-y-1">
        <h1 className="text-3xl font-semibold tracking-tight">Find my order</h1>
        <p className="text-muted-foreground">
          Lost the link to your order? Enter its code and the email you used at checkout.
        </p>
      </div>
      <FindOrderForm />
    </div>
  );
}