import type { Metadata } from "next";
import { CheckoutPage } from "@/components/checkout/checkout-page";

export const metadata: Metadata = { title: "Checkout" };

export default function Page() {
  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-semibold tracking-tight">Checkout</h1>
      <CheckoutPage />
    </div>
  );
}