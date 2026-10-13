import type { NextRequest } from "next/server";
import { NextResponse } from "next/server";
import { SESSION_COOKIE } from "@/lib/auth-constants";
import { callApiForm } from "@/lib/server/api";
import { rejectCrossOrigin } from "@/lib/server/route-helpers";

export const maxDuration = 60;

const MAX_BYTES = 4 * 1024 * 1024; // the API enforces the same limit and checks the real file type

export async function POST(request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const blocked = rejectCrossOrigin(request);
  if (blocked) return blocked;

  const { id } = await params;
  if (!/^\d+$/.test(id)) return NextResponse.json({ title: "Not found." }, { status: 404 });

  const token = request.cookies.get(SESSION_COOKIE)?.value;
  if (!token) return NextResponse.json({ title: "Please sign in." }, { status: 401 });

  let form: FormData;
  try {
    form = await request.formData();
  } catch {
    return NextResponse.json({ title: "Invalid upload." }, { status: 400 });
  }

  const file = form.get("file");
  if (!(file instanceof File)) return NextResponse.json({ title: "Please choose an image." }, { status: 400 });
  if (file.size > MAX_BYTES) {
    return NextResponse.json({ title: "Images must be 4 MB or smaller." }, { status: 413 });
  }

  const outgoing = new FormData();
  outgoing.append("file", file, file.name);

  const upstream = await callApiForm(`/api/admin/products/${id}/image`, outgoing, token);
  if (!upstream) {
    return NextResponse.json({ title: "We couldn't reach the server. Please try again." }, { status: 503 });
  }

  const result = new NextResponse(await upstream.text(), {
    status: upstream.status,
    headers: {
      "Content-Type": upstream.headers.get("content-type") ?? "application/json",
      "Cache-Control": "no-store",
    },
  });
  if (upstream.status === 401) result.cookies.delete(SESSION_COOKIE);
  return result;
}