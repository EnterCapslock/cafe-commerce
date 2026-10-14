import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api/client";
import type { OrderDto } from "@/types/api";

type Result =
  | { userId: number; attempt: number; status: "ready"; orders: OrderDto[] }
  | { userId: number; attempt: number; status: "error" };

export type MyOrdersState =
  | { status: "loading" }
  | { status: "error" }
  | { status: "ready"; orders: OrderDto[] };

/** The signed-in customer's recent orders, newest first. Pass null to do nothing. */
export function useMyOrders(userId: number | null) {
  const [result, setResult] = useState<Result | null>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    if (userId === null) return;
    const controller = new AbortController();
    api
      .getMyOrders(controller.signal)
      .then((orders) => setResult({ userId, attempt, status: "ready", orders }))
      .catch(() => {
        if (!controller.signal.aborted) setResult({ userId, attempt, status: "error" });
      });
    return () => controller.abort();
  }, [userId, attempt]);

  // A result only counts if it belongs to the current user and the current attempt.
  const state: MyOrdersState =
    result && result.userId === userId && result.attempt === attempt ? result : { status: "loading" };

  const reload = useCallback(() => setAttempt((a) => a + 1), []);
  return { state, reload };
}