import { useCallback, useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api/client";
import { toTimestamp } from "@/lib/time";
import type { OrderDto, OrderStatus } from "@/types/api";

const POLL_MS = 10_000;
const FRESH_MS = 30_000; // how long a new order keeps its highlight

export type BoardState =
  | { status: "loading" }
  | { status: "error" }
  | { status: "unauthorized" }
  | { status: "ready"; orders: OrderDto[] };

const bySoonestPickup = (a: OrderDto, b: OrderDto) =>
  toTimestamp(a.pickupTime) - toTimestamp(b.pickupTime) || toTimestamp(a.createdAt) - toTimestamp(b.createdAt);

/** Loads the active order board, keeps it fresh, and applies status changes. */
export function useOrderBoard() {
  const [state, setState] = useState<BoardState>({ status: "loading" });
  const [fresh, setFresh] = useState<ReadonlySet<string>>(() => new Set());
  const [busy, setBusy] = useState<ReadonlySet<string>>(() => new Set());
  const [stale, setStale] = useState(false);
  const [updatedAt, setUpdatedAt] = useState<number | null>(null);

  const known = useRef<Set<string> | null>(null); // order ids we've already shown
  const stopped = useRef(false); // set when the session is no longer valid

  const load = useCallback(async (signal?: AbortSignal) => {
    try {
      const orders = await api.getAdminOrders({}, signal);

      if (known.current === null) {
        known.current = new Set(orders.map((o) => o.id)); // the first load isn't "new"
      } else {
        const seen = known.current;
        const added = orders.filter((o) => !seen.has(o.id));
        if (added.length > 0) {
          added.forEach((o) => seen.add(o.id));
          setFresh((prev) => new Set([...prev, ...added.map((o) => o.id)]));
          toast.info(
            added.length === 1 ? `New order from ${added[0].customerName}` : `${added.length} new orders`,
          );
          setTimeout(() => {
            setFresh((prev) => {
              const next = new Set(prev);
              added.forEach((o) => next.delete(o.id));
              return next;
            });
          }, FRESH_MS);
        }
      }

      setState({ status: "ready", orders: [...orders].sort(bySoonestPickup) });
      setStale(false);
      setUpdatedAt(Date.now());
    } catch (error) {
      if (signal?.aborted) return;
      if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
        stopped.current = true;
        setState({ status: "unauthorized" });
        return;
      }
      setStale(true); // keep showing what we have
      setState((prev) => (prev.status === "ready" ? prev : { status: "error" }));
    }
  }, []);

  // Poll while the tab is visible; refresh right away when it becomes visible again.
  useEffect(() => {
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout> | undefined;

    const tick = async () => {
      if (stopped.current) return;
      if (document.visibilityState === "visible") await load(controller.signal);
      if (!controller.signal.aborted && !stopped.current) timer = setTimeout(tick, POLL_MS);
    };

    const onVisibility = () => {
      if (document.visibilityState === "visible" && !stopped.current) {
        clearTimeout(timer);
        void tick();
      }
    };

    document.addEventListener("visibilitychange", onVisibility);
    timer = setTimeout(tick, 0);

    return () => {
      controller.abort();
      clearTimeout(timer);
      document.removeEventListener("visibilitychange", onVisibility);
    };
  }, [load]);

  const changeStatus = useCallback(
    async (id: string, target: OrderStatus): Promise<boolean> => {
      setBusy((prev) => new Set(prev).add(id));
      try {
        const updated = await api.updateOrderStatus(id, target);

        setFresh((prev) => {
          const next = new Set(prev);
          next.delete(id);
          return next;
        });
        setState((prev) => {
          if (prev.status !== "ready") return prev;
          const rest = prev.orders.filter((o) => o.id !== id);
          const stillActive = updated.status !== "Completed" && updated.status !== "Cancelled";
          return { status: "ready", orders: (stillActive ? [...rest, updated] : rest).sort(bySoonestPickup) };
        });
        return true;
      } catch (error) {
        if (error instanceof ApiError && error.status === 409) {
          // Someone else got there first, or the move isn't allowed. Show why, then re-sync.
          toast.error(error.problem?.title ?? "That order was changed by someone else.");
          void load();
        } else if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
          stopped.current = true;
          setState({ status: "unauthorized" });
        } else {
          toast.error("We couldn't update the order. Please try again.");
        }
        return false;
      } finally {
        setBusy((prev) => {
          const next = new Set(prev);
          next.delete(id);
          return next;
        });
      }
    },
    [load],
  );

  return { state, fresh, busy, stale, updatedAt, refresh: () => load(), changeStatus };
}