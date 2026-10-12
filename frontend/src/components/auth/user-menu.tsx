"use client";

import Link from "next/link";
import { useSession } from "@/components/auth/session-provider";
import { Button } from "@/components/ui/button";
import { useLastOrderId } from "@/lib/last-order";

export function UserMenu() {
  const { session, signOut } = useSession();
  const lastOrderId = useLastOrderId();

  if (session.status === "loading") {
    return <div data-testid="session-loading" className="h-8 w-20 animate-pulse rounded-md bg-muted" aria-hidden />
  }

  if (session.status === "anonymous") {
    return (
      <div className="flex items-center gap-1">
        {lastOrderId && (
          <Button asChild variant="ghost" size="sm">
            <Link href={`/orders/${lastOrderId}`}>Track order</Link>
          </Button>
        )}
        <Button asChild variant="ghost" size="sm">
          <Link href="/login">Sign in</Link>
        </Button>
      </div>
    );
  }

  const { user } = session;
  return (
    <div className="flex items-center gap-1">
      <span className="hidden max-w-40 truncate px-2 text-sm text-muted-foreground sm:inline">
        Hi, {user.name.split(" ")[0]}
      </span>
      {user.role === "Customer" && (
        <Button asChild variant="ghost" size="sm">
          <Link href="/account/orders">My orders</Link>
        </Button>
      )}
      {user.role === "Admin" && (
        <Button asChild variant="ghost" size="sm">
          <Link href="/admin">Admin</Link>
        </Button>
      )}
      <Button variant="ghost" size="sm" onClick={() => void signOut()}>
        Sign out
      </Button>
    </div>
  );
}