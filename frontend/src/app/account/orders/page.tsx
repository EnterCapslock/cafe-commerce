import type { Metadata } from "next";
import { MyOrders } from "@/components/account/my-orders";

export const metadata: Metadata = { title: "My orders" };

export default function MyOrdersPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-semibold tracking-tight">My orders</h1>
      <MyOrders />
    </div>
  );
}