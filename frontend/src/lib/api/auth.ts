import { request } from "@/lib/api/client";
import type { UserDto } from "@/types/api";

// (calls this app's own route handlers, never the .NET API directly)
export const authApi = {
  session: (signal?: AbortSignal) =>
    request<{ user: UserDto | null }>("/api/auth/session", { baseUrl: "", cache: "no-store", signal }),

  login: (body: { email: string; password: string }) =>
    request<{ user: UserDto }>("/api/auth/login", { baseUrl: "", method: "POST", body }),

  register: (body: { name: string; email: string; password: string }) =>
    request<{ user: UserDto }>("/api/auth/register", { baseUrl: "", method: "POST", body }),

  logout: () => request<{ ok: true }>("/api/auth/logout", { baseUrl: "", method: "POST", body: {} }),
};