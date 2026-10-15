import type {
  AdminProduct,
  CreateOrderRequest,
  OrderDto,
  OrderStatus,
  ProblemDetails,
  ProductCategory,
  ProductDetail,
  ProductInput,
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
  /** A plain object is sent as JSON; a FormData is sent as multipart (for file uploads). */
  body?: unknown;
  /** Defaults to the .NET API. Pass "" to call this Next.js app's own route handlers. */
  baseUrl?: string;
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
  const { method = "GET", body, baseUrl = API_URL, revalidate, cache, signal } = options;
  const isForm = typeof FormData !== "undefined" && body instanceof FormData;

  let response: Response;
  try {
    response = await fetch(`${baseUrl}${path}`, {
      method,
      headers: {
        Accept: "application/json",
        // For FormData the browser sets the multipart header (with its boundary) itself.
        ...(body !== undefined && !isForm ? { "Content-Type": "application/json" } : {}),
      },
      body: body === undefined ? undefined : isForm ? (body as FormData) : JSON.stringify(body),
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

  // Order calls go through this app's own proxy, which attaches the signed-in user's token.
  createOrder: (body: CreateOrderRequest) =>
    request<OrderDto>("/api/proxy/orders", { baseUrl: "", method: "POST", body }),

  getOrder: (id: string, signal?: AbortSignal) =>
    request<OrderDto>(`/api/proxy/orders/${id}`, { baseUrl: "", cache: "no-store", signal }),

  getMyOrders: (signal?: AbortSignal) =>
    request<OrderDto[]>("/api/proxy/orders/mine", { baseUrl: "", cache: "no-store", signal }),

  // A guest order can be found again from the code on its page and the email used at checkout.
  lookupOrder: (body: { code: string; email: string }) =>
    request<{ orderId: string }>("/api/proxy/orders/lookup", { baseUrl: "", method: "POST", body }),

  // ---------- Admin (the API rejects anyone who isn't an admin) ----------

  /** No status = the active board. Completed or Cancelled = history. */
  getAdminOrders: (params: { status?: OrderStatus; search?: string } = {}, signal?: AbortSignal) => {
    const qs = new URLSearchParams();
    if (params.status) qs.set("status", params.status);
    if (params.search?.trim()) qs.set("search", params.search.trim());
    const query = qs.toString();
    return request<OrderDto[]>(`/api/proxy/admin/orders${query ? `?${query}` : ""}`, {
      baseUrl: "",
      cache: "no-store",
      signal,
    });
  },

  updateOrderStatus: (id: string, status: OrderStatus) =>
    request<OrderDto>(`/api/proxy/admin/orders/${id}/status`, {
      baseUrl: "",
      method: "PATCH",
      body: { status },
    }),

  getAdminProducts: (signal?: AbortSignal) =>
    request<AdminProduct[]>("/api/proxy/admin/products", { baseUrl: "", cache: "no-store", signal }),

  getAdminProduct: (id: number, signal?: AbortSignal) =>
    request<AdminProduct>(`/api/proxy/admin/products/${id}`, { baseUrl: "", cache: "no-store", signal }),

  createProduct: (body: ProductInput) =>
    request<AdminProduct>("/api/proxy/admin/products", { baseUrl: "", method: "POST", body }),

  updateProduct: (id: number, body: ProductInput) =>
    request<AdminProduct>(`/api/proxy/admin/products/${id}`, { baseUrl: "", method: "PUT", body }),

  deleteProduct: (id: number) =>
    request<void>(`/api/proxy/admin/products/${id}`, { baseUrl: "", method: "DELETE" }),

  // Files go through their own route handler, because the JSON proxy can't carry them.
  uploadProductImage: (id: number, file: File) => {
    const form = new FormData();
    form.append("file", file, file.name);
    return request<AdminProduct>(`/api/upload/products/${id}/image`, { baseUrl: "", method: "POST", body: form });
  },

  removeProductImage: (id: number) =>
    request<AdminProduct>(`/api/proxy/admin/products/${id}/image`, { baseUrl: "", method: "DELETE" }),
};