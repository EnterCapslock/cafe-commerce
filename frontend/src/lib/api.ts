import type {
  CreateOrderRequest,
  OrderDto,
  ProblemDetails,
  ProductCategory,
  ProductDetail,
  ProductSummary,
} from "@/types/api";

const API_URL = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080").replace(/\/$/, "");

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | null;

  constructor(status: number, problem: ProblemDetails | null, message: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }
}

interface RequestOptions {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  /** Server-side only: cache this response for N seconds. Don't combine with `cache`. */
  revalidate?: number;
  /** Standard fetch cache mode, e.g. "no-store" for data that must never be stale. */
  cache?: RequestCache;
  signal?: AbortSignal;
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return null;
  }
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = "GET", body, revalidate, cache, signal } = options;

  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      method,
      headers: {
        Accept: "application/json",
        ...(body !== undefined ? { "Content-Type": "application/json" } : {}),
      },
      body: body !== undefined ? JSON.stringify(body) : undefined,
      signal,
      ...(cache !== undefined ? { cache } : {}),
      ...(revalidate !== undefined ? { next: { revalidate } } : {}),
    });
  } catch {
    throw new ApiError(0, null, "Could not reach the server.");
  }

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(
      response.status,
      problem,
      problem?.title ?? `Request failed (${response.status}).`,
    );
  }

  // 204 No Content has no body. Callers of such endpoints use request<void>(...).
  if (response.status === 204) return undefined as T;

  return (await response.json()) as T;
}

export const api = {
  getMenu: (category?: ProductCategory) =>
    request<ProductSummary[]>(`/api/products${category ? `?category=${category}` : ""}`, {
      revalidate: 30,
    }),

  // Availability changes quickly, so never serve this from any cache.
  getProduct: (id: number) =>
    request<ProductDetail>(`/api/products/${id}`, { cache: "no-store" }),

  createOrder: (body: CreateOrderRequest) =>
    request<OrderDto>("/api/orders", { method: "POST", body }),

  getOrder: (id: string, signal?: AbortSignal) =>
    request<OrderDto>(`/api/orders/${id}`, { cache: "no-store", signal }),
};