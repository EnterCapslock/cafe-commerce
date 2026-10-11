"use client";

import { Check, Copy } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { Button } from "@/components/ui/button";

export function SaveOrderLink({ orderId }: { orderId: string }) {
  const [copied, setCopied] = useState(false);

  async function copy() {
    try {
      await navigator.clipboard.writeText(`${window.location.origin}/orders/${orderId}`);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      /* clipboard blocked: the link is still in the address bar */
    }
  }

  return (
    <section className="space-y-3 rounded-xl border border-dashed p-4" aria-label="Save your order link">
      <div className="space-y-1">
        <h2 className="font-medium">Save this page&apos;s link</h2>
        <p className="text-sm text-muted-foreground">
          You&apos;re checking out as a guest, so this page is the only way to check on your order.
          Bookmark it or copy the link.
        </p>
      </div>
      <div className="flex flex-wrap items-center gap-3">
        <Button type="button" variant="outline" size="sm" onClick={() => void copy()}>
          {copied ? <Check aria-hidden /> : <Copy aria-hidden />}
          {copied ? "Link copied" : "Copy link"}
        </Button>
        <span role="status" className="sr-only">{copied ? "Link copied to clipboard" : ""}</span>
        <p className="text-sm text-muted-foreground">
          For future orders,{" "}
          <Link href="/register" className="font-medium text-foreground underline underline-offset-4">
            create an account
          </Link>{" "}
          to see them all in one place.
        </p>
      </div>
    </section>
  );
}