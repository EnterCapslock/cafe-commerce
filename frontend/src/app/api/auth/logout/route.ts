import type { NextRequest } from "next/server";
import { NextResponse } from "next/server";
import { SESSION_COOKIE } from "@/lib/auth-constants";
import { rejectCrossOrigin } from "@/lib/server/route-helpers";

export async function POST(request: NextRequest) {
  const blocked = rejectCrossOrigin(request);
  if (blocked) return blocked;

  const response = NextResponse.json({ ok: true });
  response.cookies.delete(SESSION_COOKIE);
  return response;
}