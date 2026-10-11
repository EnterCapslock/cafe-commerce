import type { NextRequest } from "next/server";
import { NextResponse } from "next/server";
import { authenticate, readJsonBody, rejectCrossOrigin, str } from "@/lib/server/route-helpers";

export const maxDuration = 60; // lets a cold-starting API finish on hosts with function time limits

export async function POST(request: NextRequest) {
  const blocked = rejectCrossOrigin(request);
  if (blocked) return blocked;

  const body = await readJsonBody(request);
  if (!body) return NextResponse.json({ title: "Invalid request." }, { status: 400 });

  // Pick fields explicitly; never forward arbitrary client input.
  return authenticate(request, "/api/auth/login", {
    email: str(body.email),
    password: str(body.password),
  });
}