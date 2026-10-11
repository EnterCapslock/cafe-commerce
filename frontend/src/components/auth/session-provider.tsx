"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
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

  const signIn = useCallback((user: UserDto) => setSession({ status: "authenticated", user }), []);

  const signOut = useCallback(async () => {
    try {
      await authApi.logout();
    } finally {
      setSession({ status: "anonymous" });
    }
  }, []);

  const value = useMemo(() => ({ session, signIn, signOut }), [session, signIn, signOut]);
  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionContextValue {
  const context = useContext(SessionContext);
  if (!context) throw new Error("useSession must be used inside <SessionProvider>.");
  return context;
}