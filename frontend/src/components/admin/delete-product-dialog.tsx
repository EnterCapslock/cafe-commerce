"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from "@/components/ui/dialog";
import { api, ApiError } from "@/lib/api/client";
import type { AdminProduct } from "@/types/api";

interface Props {
  product: AdminProduct | null;
  onClose: () => void;
  onDeleted: (product: AdminProduct) => void;
  onArchive: (product: AdminProduct) => Promise<void>;
}

// Rendered with key={product id}, so each open starts fresh.
export function DeleteProductDialog({ product, onClose, onDeleted, onArchive }: Props) {
  const [submitting, setSubmitting] = useState(false);
  const [conflict, setConflict] = useState<string | null>(null);

  async function confirm() {
    if (!product) return;
    setSubmitting(true);
    try {
      await api.deleteProduct(product.id);
      toast.success(`${product.name} deleted`);
      onDeleted(product);
      onClose();
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        setConflict(error.problem?.title ?? "This item has past orders, so it can't be deleted.");
      } else {
        toast.error("We couldn't delete the item. Please try again.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  async function archive() {
    if (!product) return;
    setSubmitting(true);
    try {
      await onArchive(product);
    } finally {
      setSubmitting(false);
      onClose();
    }
  }

  return (
    <Dialog open={product !== null} onOpenChange={(open) => { if (!open && !submitting) onClose(); }}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{conflict ? "This item can't be deleted" : `Delete ${product?.name}?`}</DialogTitle>
          <DialogDescription>
            {conflict ??
              "This permanently removes the item and its options from the menu. It can't be undone."}
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" disabled={submitting} onClick={onClose}>
            {conflict ? "Close" : "Keep item"}
          </Button>
          {conflict ? (
            <Button disabled={submitting} onClick={() => void archive()}>
              {submitting ? "Archiving…" : "Archive instead"}
            </Button>
          ) : (
            <Button variant="destructive" disabled={submitting} onClick={() => void confirm()}>
              {submitting ? "Deleting…" : "Delete"}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}