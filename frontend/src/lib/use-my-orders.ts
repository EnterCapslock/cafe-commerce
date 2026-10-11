import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { OrderDto } from "@/types/api";

type MyOrdersState =
  | { status: "loading" }
  | { status: "error" }
  | { status: "ready"; orders: OrderDto[] };

/** The signed-in customer's recent orders, newest first. Does nothing until `enabled`. */
export function useMyOrders(enabled: boolean) {
  const [state, setState] = useState<MyOrdersState>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    if (!enabled) return;
    const controller = new AbortController();
    api
      .getMyOrders(controller.signal)
      .then((orders) => setState({ status: "ready", orders }))
      .catch(() => {
        if (!controller.signal.aborted) setState({ status: "error" });
      });
    return () => controller.abort();
  }, [enabled, attempt]);

  const reload = useCallback(() => {
    setState({ status: "loading" });
    setAttempt((a) => a + 1);
  }, []);

  return { state, reload };
}