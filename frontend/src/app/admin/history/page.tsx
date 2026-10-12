import type { Metadata } from "next";
import { OrderHistory } from "@/components/admin/order-history";

export const metadata: Metadata = { title: "Order history" };

export default function AdminHistoryPage() {
  return <OrderHistory />;
}