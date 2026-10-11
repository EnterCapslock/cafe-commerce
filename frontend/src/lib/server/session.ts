import "server-only";
import { cookies } from "next/headers";
import { SESSION_COOKIE } from "@/lib/auth-constants";
import { callApi } from "@/lib/server/api";
import type { UserDto } from "@/types/api";

/** The signed-in user, verified by the API itself (not just "a cookie exists"). */
export async function getCurrentUser(): Promise<UserDto | null> {
  const token = (await cookies()).get(SESSION_COOKIE)?.value;
  if (!token) return null;

  const response = await callApi("/api/auth/me", { token });
  if (!response?.ok) return null;
  return (await response.json()) as UserDto;
}