import { request } from "@/lib/api";

export interface VariantCheck {
  variantId: number;
  available: boolean;
  price: number | null;
}

export const checkCart = (variantIds: number[], signal?: AbortSignal) =>
  request<VariantCheck[]>("/api/products/check", {
    method: "POST",
    body: { variantIds },
    cache: "no-store",
    signal,
  });