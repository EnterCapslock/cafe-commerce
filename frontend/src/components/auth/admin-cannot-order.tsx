"use client";

import { ShieldCheck } from "lucide-react";
import Link from "next/link";
import { useSession } from "@/components/auth/session-provider";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { useShopper } from "@/lib/use-shopper";

/** Full-page message for /cart and /checkout. */
export function AdminCannotOrder() {
  const { signOut } = useSession();

  return (
    <div className="mx-auto max-w-md space-y-4 py-16 text-center">
      <h2 className="text-xl font-medium">Admin accounts can&apos;t place orders</h2>
      <p className="text-muted-foreground">
        You&apos;re signed in as staff. To order, sign out and sign in with a customer account.
      </p>
      <div className="flex justify-center gap-2">
        <Button asChild>
          <Link href="/admin">Go to admin</Link>
        </Button>
      <Button variant="outline" onClick={() => void signOut()}>
        Sign out
      </Button>
      </div>
    </div>
  );
}

/** Small banner for the menu page. */
export function AdminOrderingNotice() {
  const { isAdmin } = useShopper();
  if (!isAdmin) return null;

  return (
    <Alert>
      <ShieldCheck aria-hidden />
      <AlertTitle>Ordering is turned off for admin accounts</AlertTitle>
      <AlertDescription>Sign in with a customer account to place an order.</AlertDescription>
    </Alert>
  );
}