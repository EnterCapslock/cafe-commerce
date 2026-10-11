import "server-only";
import { NextResponse, type NextRequest } from "next/server";
import { SESSION_COOKIE } from "@/lib/auth-constants";
import { callApi } from "@/lib/server/api";
import type { AuthResponse } from "@/types/api";

/** Blocks state-changing requests that come from another site. */
export function rejectCrossOrigin(request: NextRequest): NextResponse | null {
  const origin = request.headers.get("origin");
  if (!origin) return null; // same-origin navigations don't always send it
  try {
    if (new URL(origin).host === request.headers.get("host")) return null;
  } catch {
    /* malformed origin falls through to the rejection */
  }
  return NextResponse.json({ title: "Cross-origin request blocked." }, { status: 403 });
}

export async function readJsonBody(request: NextRequest): Promise<Record<string, unknown> | null> {
  try {
    const body: unknown = await request.json();
    return body && typeof body === "object" ? (body as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

export const str = (value: unknown): string => (typeof value === "string" ? value : "");

export function clientIp(request: NextRequest): string | null {
  return request.headers.get("x-forwarded-for")?.split(",")[0]?.trim() || null;
}

/** Forwards credentials to the API; on success stores the JWT in an httpOnly cookie. */
export async function authenticate(
  request: NextRequest,
  apiPath: string,
  payload: Record<string, string>,
): Promise<NextResponse> {
  const response = await callApi(apiPath, { method: "POST", body: payload, clientIp: clientIp(request) });

  if (!response) {
    return NextResponse.json(
      { title: "We couldn't reach the server. Please try again." },
      { status: 503 },
    );
  }

  if (!response.ok) {
    // Pass the API's problem details (400, 401, 409, 429...) straight through.
    const problem: unknown = await response.json().catch(() => ({ title: "Request failed." }));
    return NextResponse.json(problem, { status: response.status });
  }

  const auth = (await response.json()) as AuthResponse;
  const result = NextResponse.json({ user: auth.user }, { status: response.status });
  result.cookies.set({
    name: SESSION_COOKIE,   
    value: auth.token,
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    expires: new Date(auth.expiresAt),
  });
  return result;
}