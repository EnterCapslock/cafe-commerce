import type { Metadata } from "next";
import { Suspense } from "react";
import { LoginForm } from "@/components/auth/login-form";
import { safeNext } from "@/lib/safe-next";

export const metadata: Metadata = { title: "Sign in" };

type SearchParams = Promise<{ next?: string | string[] }>;

export default function LoginPage({ searchParams }: { searchParams: SearchParams }) {
  return (
    <div className="mx-auto w-full max-w-sm space-y-6">
      <div className="space-y-1">
        <h1 className="text-3xl font-semibold tracking-tight">Sign in</h1>
        <p className="text-muted-foreground">Welcome back.</p>
      </div>
      <Suspense fallback={<div className="h-64 animate-pulse rounded-xl bg-muted" aria-busy="true" />}>
        <LoginContent searchParams={searchParams} />
      </Suspense>
    </div>
  );
}

async function LoginContent({ searchParams }: { searchParams: SearchParams }) {
  return <LoginForm next={safeNext((await searchParams).next)} />;
}