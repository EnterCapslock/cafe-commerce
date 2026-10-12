import type { Metadata } from "next";
import { OrderBoard } from "@/components/admin/order-board";

export const metadata: Metadata = { title: "Orders" };

export default function AdminOrdersPage() {
  return <OrderBoard />;
}