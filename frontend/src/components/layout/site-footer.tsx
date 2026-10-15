import Link from "next/link";
import { SITE_NAME } from "@/lib/config";

export function SiteFooter() {
  return (
    <footer className="border-t">
      <div className="mx-auto flex w-full max-w-6xl flex-wrap items-center justify-between gap-3 px-4 py-6 text-sm text-muted-foreground">
        <p>{SITE_NAME}: order ahead, pay online or at the counter.</p>
        <nav aria-label="Footer" className="flex gap-4">
          <Link href="/" className="hover:text-foreground">Menu</Link>
          <Link href="/find-order" className="hover:text-foreground">Find my order</Link>
        </nav>
      </div>
    </footer>
  );
}