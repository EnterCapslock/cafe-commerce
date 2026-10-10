import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";

export const MAX_LINE_QUANTITY = 20; // keep in sync with Orders:MaxQuantityPerLine on the API

export interface CartItem {
  variantId: number;
  productId: number;
  productName: string;
  variantName: string;
  unitPrice: number;
  quantity: number;
}

type NewCartItem = Omit<CartItem, "quantity">;

interface CartState {
  items: CartItem[];
  add: (item: NewCartItem, quantity?: number) => void;
  setQuantity: (variantId: number, quantity: number) => void;
  remove: (variantId: number) => void;
  clear: () => void;
}

const clamp = (n: number) => Math.min(MAX_LINE_QUANTITY, Math.max(1, Math.floor(n)));

export const useCartStore = create<CartState>()(
  persist(
    (set) => ({
      items: [],

      add: (item, quantity = 1) =>
        set((state) => {
          const existing = state.items.find((i) => i.variantId === item.variantId);
          if (existing) {
            return {
              items: state.items.map((i) =>
                i.variantId === item.variantId
                  ? { ...i, unitPrice: item.unitPrice, quantity: clamp(i.quantity + quantity) }
                  : i,
              ),
            };
          }
          return { items: [...state.items, { ...item, quantity: clamp(quantity) }] };
        }),

      setQuantity: (variantId, quantity) =>
        set((state) => ({
          items:
            quantity < 1
              ? state.items.filter((i) => i.variantId !== variantId)
              : state.items.map((i) =>
                  i.variantId === variantId ? { ...i, quantity: clamp(quantity) } : i,
                ),
        })),

      remove: (variantId) =>
        set((state) => ({ items: state.items.filter((i) => i.variantId !== variantId) })),

      clear: () => set({ items: [] }),
    }),
    {
      name: "cafe-cart",
      version: 1,
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({ items: state.items }),
    },
  ),
);

// Selectors return primitives, so components only re-render when the value changes.
export const selectCount = (s: CartState) => s.items.reduce((n, i) => n + i.quantity, 0);
export const selectSubtotal = (s: CartState) =>
  s.items.reduce((sum, i) => sum + i.unitPrice * i.quantity, 0);