import type { Metadata } from "next";

export const metadata: Metadata = { title: "Admin" };

export default function AdminHome() {
  return (
    <div className="space-y-2">
      <h1 className="text-3xl font-semibold tracking-tight">Admin dashboard</h1>
      <p className="text-muted-foreground">
        Menu management and the live order board arrive in Milestone 6.
      </p>
    </div>
  );
}