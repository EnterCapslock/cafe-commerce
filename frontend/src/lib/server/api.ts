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

const PROXY_SECRET = process.env.PROXY_SHARED_SECRET ?? "";

/** Calls the .NET API from the Next.js server. Returns null if it can't be reached. */
export async function callApi(path: string, opts: CallOptions = {}): Promise<Response | null> {
  try {
    return await fetch(`${API_URL}${path}`, {
      method: opts.method ?? "GET",
      headers: {
        Accept: "application/json",
        ...(opts.body !== undefined ? { "Content-Type": "application/json" } : {}),
        ...(opts.token ? { Authorization: `Bearer ${opts.token}` } : {}),
        // Lets the API rate-limit each visitor separately, instead of treating every request as coming from this server
        ...(opts.clientIp && PROXY_SECRET
          ? { "X-Cafe-Proxy-Secret": PROXY_SECRET, "X-Cafe-Client-Ip": opts.clientIp }
          : {}),
      },
      body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
      cache: "no-store",
      signal: AbortSignal.timeout(55_000), // free-tier APIs can take up to a minute to wake
    });
  } catch {
    return null;
  }
}

/** Sends a multipart form to the .NET API from the Next.js server. Returns null if it can't be reached. */
export async function callApiForm(path: string, form: FormData, token: string): Promise<Response | null> {
  try {
    return await fetch(`${API_URL}${path}`, {
      method: "POST",
      headers: { Accept: "application/json", Authorization: `Bearer ${token}` }, // fetch sets the multipart header
      body: form,
      cache: "no-store",
      signal: AbortSignal.timeout(55_000),
    });
  } catch {
    return null;
  }
}