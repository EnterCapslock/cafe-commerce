"use client";

import { Button } from "@/components/ui/button";

export default function ErrorPage({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <div className="mx-auto max-w-md space-y-4 py-16 text-center">
      <h1 className="text-2xl font-semibold">We couldn&apos;t load the menu</h1>
      <p className="text-muted-foreground">
        The server may still be waking up, or you may be offline. Please try again.
      </p>
      <Button onClick={reset}>Try again</Button>
    </div>
  );
}