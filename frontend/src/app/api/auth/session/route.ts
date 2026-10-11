import type { NextRequest } from "next/server";
import { NextResponse } from "next/server";
import { SESSION_COOKIE } from "@/lib/auth-constants";
import { callApi } from "@/lib/server/api";

export const maxDuration = 60;

export async function GET(request: NextRequest) {
  const token = request.cookies.get(SESSION_COOKIE)?.value;
  if (!token) return NextResponse.json({ user: null });

  const response = await callApi("/api/auth/me", { token });
  if (!response) return NextResponse.json({ user: null }, { status: 503 }); // keep the cookie; the API may just be asleep

  if (response.status === 401) {
    // Expired, invalid, or the account no longer exists: drop the cookie.
    const result = NextResponse.json({ user: null });
    result.cookies.delete(SESSION_COOKIE);
    return result;
  }

  if (!response.ok) return NextResponse.json({ user: null }, { status: 502 });
  return NextResponse.json({ user: await response.json() });
}