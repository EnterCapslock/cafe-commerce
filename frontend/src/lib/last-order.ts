import { useMemo, useSyncExternalStore } from "react";

const KEY = "cafe-recent-orders";
const EVENT = "cafe-recent-orders-changed";
const MAX_ORDERS = 5;
const MAX_AGE_MS = 3 * 24 * 60 * 60 * 1000;

interface Entry {
  id: string;
  at: number;
}

function readEntries(): Entry[] {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(KEY) ?? "[]");
    if (!Array.isArray(parsed)) return [];
    const cutoff = Date.now() - MAX_AGE_MS;
    return parsed.filter(
      (e): e is Entry => typeof e?.id === "string" && typeof e?.at === "number" && e.at > cutoff,
    );
  } catch {
    return [];
  }
}

export function rememberOrder(id: string): void {
  try {
    const entries = [{ id, at: Date.now() }, ...readEntries().filter((e) => e.id !== id)].slice(0, MAX_ORDERS);
    localStorage.setItem(KEY, JSON.stringify(entries));
    window.dispatchEvent(new Event(EVENT)); // the "storage" event only fires in other tabs
  } catch {
    /* storage unavailable (private mode); the links just won't be remembered */
  }
}

function subscribe(callback: () => void) {
  window.addEventListener("storage", callback);
  window.addEventListener(EVENT, callback);
  return () => {
    window.removeEventListener("storage", callback);
    window.removeEventListener(EVENT, callback);
  };
}

const snapshot = () => JSON.stringify(readEntries().map((e) => e.id));

/** Recent order IDs on this device, newest first. Empty on the server and during hydration. */
export function useRecentOrderIds(): string[] {
  const raw = useSyncExternalStore(subscribe, snapshot, () => "[]");
  return useMemo(() => JSON.parse(raw) as string[], [raw]);
}

// Names used by the checkout page since the last-order link was added.
export const rememberLastOrder = rememberOrder;

export function useLastOrderId(): string | null {
  return useRecentOrderIds()[0] ?? null;
}