import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Suspense } from "react";
import { OrderTracker } from "@/components/orders/order-tracker";

export const metadata: Metadata = { title: "Your order" };

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export default function OrderPage({ params }: { params: Promise<{ id: string }> }) {
  return (
    <Suspense fallback={<div className="h-64 animate-pulse rounded-xl bg-muted" aria-busy="true" />}>
      <OrderContent params={params} />
    </Suspense>
  );
}

async function OrderContent({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!GUID.test(id)) notFound();
  return <OrderTracker id={id} />;
}