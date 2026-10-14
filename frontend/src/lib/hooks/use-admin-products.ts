import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api/client";
import type { AdminProduct } from "@/types/api";

type State =
  | { status: "loading" }
  | { status: "error" }
  | { status: "unauthorized" }
  | { status: "ready"; products: AdminProduct[] };

export function useAdminProducts() {
  const [state, setState] = useState<State>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    api
      .getAdminProducts(controller.signal)
      .then((products) => setState({ status: "ready", products }))
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        const unauthorized = error instanceof ApiError && (error.status === 401 || error.status === 403);
        setState({ status: unauthorized ? "unauthorized" : "error" });
      });
    return () => controller.abort();
  }, [attempt]);

  const reload = useCallback(() => {
    setState({ status: "loading" });
    setAttempt((a) => a + 1);
  }, []);

  const upsert = useCallback(
    (product: AdminProduct) =>
      setState((prev) =>
        prev.status === "ready"
          ? { status: "ready", products: prev.products.map((p) => (p.id === product.id ? product : p)) }
          : prev,
      ),
    [],
  );

  const remove = useCallback(
    (id: number) =>
      setState((prev) =>
        prev.status === "ready" ? { status: "ready", products: prev.products.filter((p) => p.id !== id) } : prev,
      ),
    [],
  );

  return { state, reload, upsert, remove };
}