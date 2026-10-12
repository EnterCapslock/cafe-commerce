import type { NextRequest } from "next/server";
import { NextResponse } from "next/server";
import { SESSION_COOKIE } from "@/lib/auth-constants";
import { callApi } from "@/lib/server/api";
import { clientIp, readJsonBody, rejectCrossOrigin } from "@/lib/server/route-helpers";

export const maxDuration = 60;

// Only these API areas can be reached through the proxy. Admin endpoints still need an admin token:
// the API enforces that on every request, whatever reaches it.
const ALLOWED_PREFIXES = ["orders", "admin"];
const METHODS_WITH_BODY = new Set(["POST", "PUT", "PATCH"]);

type Context = { params: Promise<{ path: string[] }> };

async function forward(request: NextRequest, { params }: Context): Promise<NextResponse> {
  const { path } = await params;

  // Reject "." / ".." segments, which could escape the allowlist once the URL is normalized.
  const suspicious = path.some((s) => s === "." || s === ".." || s.includes("/") || s.includes("\\"));
  if (path.length === 0 || suspicious || !ALLOWED_PREFIXES.includes(path[0])) {
    return NextResponse.json({ title: "Not found." }, { status: 404 });
  }

  let body: Record<string, unknown> | undefined;
  if (request.method !== "GET") {
    const blocked = rejectCrossOrigin(request);
    if (blocked) return blocked;

    if (METHODS_WITH_BODY.has(request.method)) {
      const parsed = await readJsonBody(request);
      if (!parsed) return NextResponse.json({ title: "Invalid request." }, { status: 400 });
      body = parsed;
    }
  }

  const token = request.cookies.get(SESSION_COOKIE)?.value;
  const target = `/api/${path.map(encodeURIComponent).join("/")}${request.nextUrl.search}`;
  const upstream = await callApi(target, {
    method: request.method,
    body,
    token,
    clientIp: clientIp(request),
  });

  if (!upstream) {
    return NextResponse.json(
      { title: "We couldn't reach the server. Please try again." },
      { status: 503 },
    );
  }

  const result =
    upstream.status === 204
      ? new NextResponse(null, { status: 204 })
      : new NextResponse(await upstream.text(), {
          status: upstream.status,
          headers: {
            "Content-Type": upstream.headers.get("content-type") ?? "application/json",
            "Cache-Control": "no-store",
          },
        });

  // The API rejected the token (expired, or the account is gone): drop the cookie.
  if (token && upstream.status === 401) result.cookies.delete(SESSION_COOKIE);
  return result;
}

export const GET = forward;
export const POST = forward;
export const PUT = forward;
export const PATCH = forward;
export const DELETE = forward;