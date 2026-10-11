import { NextResponse, type NextRequest } from "next/server";
import { SESSION_COOKIE } from "@/lib/auth-constants";

// A fast redirect for signed-out visitors. This is a convenience, not security:
// the real checks are in the admin layout and, above all, in the API itself.
export function proxy(request: NextRequest) {
  if (request.cookies.has(SESSION_COOKIE)) return NextResponse.next();

  const login = new URL("/login", request.url);
  login.searchParams.set("next", request.nextUrl.pathname + request.nextUrl.search);
  return NextResponse.redirect(login);
}

export const config = {
  matcher: ["/admin/:path*", "/account/:path*"], // /account is used by "My orders" in 5.4
};