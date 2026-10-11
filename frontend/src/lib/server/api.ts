import "server-only";

export const API_URL = (
  process.env.API_URL ?? process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080"
).replace(/\/$/, "");

interface CallOptions {
  method?: string;
  body?: unknown;
  token?: string;
  clientIp?: string | null;
}

/** Calls the .NET API from the Next.js server. Returns null if it can't be reached. */
export async function callApi(path: string, opts: CallOptions = {}): Promise<Response | null> {
  try {
    return await fetch(`${API_URL}${path}`, {
      method: opts.method ?? "GET",
      headers: {
        Accept: "application/json",
        ...(opts.body !== undefined ? { "Content-Type": "application/json" } : {}),
        ...(opts.token ? { Authorization: `Bearer ${opts.token}` } : {}),
        ...(opts.clientIp ? { "X-Forwarded-For": opts.clientIp } : {}),
      },
      body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
      cache: "no-store",
      signal: AbortSignal.timeout(55_000), // free-tier APIs can take up to a minute to wake
    });
  } catch {
    return null;
  }
}