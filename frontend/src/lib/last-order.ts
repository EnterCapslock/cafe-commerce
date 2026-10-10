import { useSyncExternalStore } from "react";

const KEY = "cafe-last-order";

export function rememberLastOrder(id: string): void {
  try {
    sessionStorage.setItem(KEY, id);
  } catch {
    /* storage unavailable (private mode); the link just won't show */
  }
}

const subscribe = () => () => {};

/** The most recent order placed in this browser tab, or null. */
export function useLastOrderId(): string | null {
  return useSyncExternalStore(
    subscribe,
    () => {
      try {
        return sessionStorage.getItem(KEY);
      } catch {
        return null;
      }
    },
    () => null,
  );
}