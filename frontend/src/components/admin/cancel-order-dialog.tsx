"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from "@/components/ui/dialog";
import type { OrderDto } from "@/types/api";

interface Props {
  order: OrderDto | null;
  onClose: () => void;
  onConfirm: (order: OrderDto) => Promise<void>;
}

export function CancelOrderDialog({ order, onClose, onConfirm }: Props) {
  const [submitting, setSubmitting] = useState(false);
  const paidOnline = order?.paymentMethod === "Online" && order.paymentStatus === "Paid";

  async function confirm() {
    if (!order) return;
    setSubmitting(true);
    try {
      await onConfirm(order);
    } finally {
      setSubmitting(false);
      onClose();
    }
  }

  return (
    <Dialog open={order !== null} onOpenChange={(open) => { if (!open && !submitting) onClose(); }}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Cancel order #{order?.id.slice(0, 8).toUpperCase()}?</DialogTitle>
          <DialogDescription>
            {paidOnline
              ? "This order was already paid online. Cancelling it means the customer is owed a refund, which you issue from the PayMongo dashboard."
              : "The customer will see their order as cancelled. This can't be undone."}
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" disabled={submitting} onClick={onClose}>
            Keep order
          </Button>
          <Button variant="destructive" disabled={submitting} onClick={() => void confirm()}>
            {submitting ? "Cancelling…" : "Cancel order"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}