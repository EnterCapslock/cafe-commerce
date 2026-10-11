import type { Metadata } from "next";
import { CartView } from "@/components/cart/cart-view";
import { UnpaidOrderNotice } from "@/components/orders/unpaid-order-notice";

export const metadata: Metadata = { title: "Your cart" };

export default function CartPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-semibold tracking-tight">Your cart</h1>
      <UnpaidOrderNotice />
      <CartView />
    </div>
  );
}