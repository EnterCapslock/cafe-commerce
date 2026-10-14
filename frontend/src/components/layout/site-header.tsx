import Link from "next/link";
import { Coffee } from "lucide-react";
import { UserMenu } from "@/components/auth/user-menu";
import { CartButton } from "@/components/layout/cart-button";
import { SITE_NAME } from "@/lib/config";

export function SiteHeader() {
  return (
    <header className="sticky top-0 z-40 border-b bg-background/90 backdrop-blur">
      <div className="mx-auto flex h-16 w-full max-w-6xl items-center justify-between px-4">
        <Link href="/" className="flex items-center gap-2 font-semibold">
          <Coffee className="size-5" aria-hidden />
          {SITE_NAME}
        </Link>
        <div className="flex items-center gap-2">
          <UserMenu />
          <CartButton />
        </div>
      </div>
    </header>
  );
}