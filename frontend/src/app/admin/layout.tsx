import Link from "next/link";
import { redirect } from "next/navigation";
import { Suspense, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { getCurrentUser } from "@/lib/server/session";

export default function AdminLayout({ children }: { children: ReactNode }) {
  return (
    <Suspense fallback={<div className="h-48 animate-pulse rounded-xl bg-muted" aria-busy="true" />}>
      <AdminGate>{children}</AdminGate>
    </Suspense>
  );
}

// Asks the API who the user is. The API also enforces the Admin role on every admin endpoint,
// so this layout is about showing the right screen, not about protecting data.
async function AdminGate({ children }: { children: ReactNode }) {
  const user = await getCurrentUser();
  if (!user) redirect("/login?next=/admin");

  if (user.role !== "Admin") {
    return (
      <div className="mx-auto max-w-md space-y-4 py-16 text-center">
        <h1 className="text-2xl font-semibold">Admin access only</h1>
        <p className="text-muted-foreground">Your account doesn&apos;t have permission to view this page.</p>
        <Button asChild>
          <Link href="/">Back to the menu</Link>
        </Button>
      </div>
    );
  }

  return <>{children}</>;
}