import type { Metadata } from "next";
import { Suspense } from "react";
import { RegisterForm } from "@/components/auth/register-form";
import { safeNext } from "@/lib/safe-next";

export const metadata: Metadata = { title: "Create account" };

type SearchParams = Promise<{ next?: string | string[] }>;

export default function RegisterPage({ searchParams }: { searchParams: SearchParams }) {
  return (
    <div className="mx-auto w-full max-w-sm space-y-6">
      <div className="space-y-1">
        <h1 className="text-3xl font-semibold tracking-tight">Create an account</h1>
        <p className="text-muted-foreground">Track your orders and check out faster.</p>
      </div>
      <Suspense fallback={<div className="h-80 animate-pulse rounded-xl bg-muted" aria-busy="true" />}>
        <RegisterContent searchParams={searchParams} />
      </Suspense>
    </div>
  );
}

async function RegisterContent({ searchParams }: { searchParams: SearchParams }) {
  return <RegisterForm next={safeNext((await searchParams).next)} />;
}