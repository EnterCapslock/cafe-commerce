"use client";

import { CreditCard } from "lucide-react";
import Link from "next/link";
import { useSession } from "@/components/auth/session-provider";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { formatPeso } from "@/lib/format";
import { useMyOrders } from "@/lib/use-my-orders";

/** For signed-in customers only: reminds them of an online order that's still waiting for payment. */
export function UnpaidOrderNotice() {
  const { session } = useSession();
  const { state } = useMyOrders(session.status === "authenticated");

  if (session.status !== "authenticated" || state.status !== "ready") return null;

  const unpaid = state.orders.filter(
    (o) => o.paymentMethod === "Online" && o.paymentStatus === "Unpaid" && o.status !== "Cancelled",
  );
  if (unpaid.length === 0) return null;

  const latest = unpaid[0]; // the API returns newest first
  return (
    <Alert>
      <CreditCard aria-hidden />
      <AlertTitle>
        {unpaid.length === 1 ? "You have an unpaid order" : `You have ${unpaid.length} unpaid orders`}
      </AlertTitle>
      <AlertDescription>
        <p>Your latest one is {formatPeso(latest.totalAmount)}. Unpaid orders are cancelled after a short time.</p>
        <Button asChild size="sm" className="mt-3">
          <Link href={`/orders/${latest.id}`}>Finish paying</Link>
        </Button>
      </AlertDescription>
    </Alert>
  );
}