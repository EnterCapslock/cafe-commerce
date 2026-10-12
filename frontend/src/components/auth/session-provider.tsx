"use client";

import { Loader2 } from "lucide-react";
import {
  createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode,
} from "react";
import { toast } from "sonner";
import { authApi } from "@/lib/auth-api";
import type { UserDto } from "@/types/api";

type SessionState =
  | { status: "loading" }
  | { status: "anonymous" }
  | { status: "authenticated"; user: UserDto };

interface SessionContextValue {
  session: SessionState;
  signIn: (user: UserDto) => void;
  signOut: () => Promise<void>;
}

const SessionContext = createContext<SessionContextValue | null>(null);

export function SessionProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<SessionState>({ status: "loading" });
  const [signingOut, setSigningOut] = useState(false);
  const leaving = useRef(false);

  useEffect(() => {
    const controller = new AbortController();
    authApi
      .session(controller.signal)
      .then(({ user }) => setSession(user ? { status: "authenticated", user } : { status: "anonymous" }))
      .catch(() => {
        if (!controller.signal.aborted) setSession({ status: "anonymous" });
      });
    return () => controller.abort();
  }, []);

  // If the browser restores a page from its back/forward cache after we signed out, it would show
  // stale signed-in screens. Reload so the page reflects the real session.
  useEffect(() => {
    function onPageShow(event: PageTransitionEvent) {
      if (event.persisted && leaving.current) window.location.reload();
    }
    window.addEventListener("pageshow", onPageShow);
    return () => window.removeEventListener("pageshow", onPageShow);
  }, []);

  const signIn = useCallback((user: UserDto) => setSession({ status: "authenticated", user }), []);

  const signOut = useCallback(async () => {
    if (leaving.current) return;
    leaving.current = true;
    setSigningOut(true); // covers the page, so no signed-out screen can flash

    try {
      await authApi.logout();
    } catch {
      leaving.current = false;
      setSigningOut(false);
      toast.error("We couldn't sign you out. Please try again.");
      return;
    }

    // A full page load on purpose: it clears everything the signed-in user had in memory and
    // lands on the menu. router.push() would keep that state alive, which is what we're avoiding.
    // eslint-disable-next-line @next/next/no-location-assign-relative-destination
    window.location.assign("/");
  }, []);

  const value = useMemo(() => ({ session, signIn, signOut }), [session, signIn, signOut]);

  return (
    <SessionContext.Provider value={value}>
      {children}
      {signingOut && (
        <div
          role="status"
          aria-live="polite"
          className="fixed inset-0 z-50 flex flex-col items-center justify-center gap-3 bg-background/90 backdrop-blur-sm"
        >
          <Loader2 className="size-8 animate-spin text-muted-foreground motion-reduce:animate-none" aria-hidden />
          <p className="text-sm font-medium">Signing you out…</p>
        </div>
      )}
    </SessionContext.Provider>
  );
}

export function useSession(): SessionContextValue {
  const context = useContext(SessionContext);
  if (!context) throw new Error("useSession must be used inside <SessionProvider>.");
  return context;
}