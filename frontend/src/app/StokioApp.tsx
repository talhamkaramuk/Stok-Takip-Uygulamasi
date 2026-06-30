import { useCallback, useEffect, useMemo, useState } from "react";
import { createApiClient } from "../shared/api/client";
import type { Notice } from "../shared/types/ui";
import type { AuthResponse } from "../types";
import { AppShell } from "./AppShell";
import { AuthScreen } from "./auth/AuthScreen";
import { clearStoredAuth } from "./auth/session";

export default function StokioApp() {
  const [auth, setAuth] = useState<AuthResponse | null>(null);
  const [isRestoringSession, setIsRestoringSession] = useState(true);
  const [notice, setNotice] = useState<Notice | null>(null);
  const api = useMemo(() => createApiClient(auth?.accessToken ?? null), [auth?.accessToken]);

  const refreshSession = useCallback(async () => {
    try {
      const response = await createApiClient(null).refreshSession();
      clearStoredAuth();
      setAuth(response);
      return true;
    } catch {
      clearStoredAuth();
      setAuth(null);
      return false;
    }
  }, []);

  useEffect(() => {
    let cancelled = false;

    async function restoreSession() {
      clearStoredAuth();
      try {
        const response = await createApiClient(null).refreshSession();
        if (!cancelled) {
          setAuth(response);
        }
      } catch {
        if (!cancelled) {
          setAuth(null);
        }
      } finally {
        if (!cancelled) {
          setIsRestoringSession(false);
        }
      }
    }

    void restoreSession();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!auth) {
      return undefined;
    }

    const expiresInMs = Date.parse(auth.expiresAt) - Date.now();
    if (!Number.isFinite(expiresInMs) || expiresInMs <= 0) {
      void refreshSession();
      return undefined;
    }

    const timeout = window.setTimeout(() => {
      void refreshSession();
    }, Math.min(Math.max(expiresInMs - 60_000, 0), 2_147_483_647));

    return () => {
      window.clearTimeout(timeout);
    };
  }, [auth, refreshSession]);

  function handleAuth(response: AuthResponse) {
    setNotice(null);
    clearStoredAuth();
    setAuth(response);
  }

  async function logout() {
    setNotice(null);
    try {
      await createApiClient(null).logout();
    } catch {
      // Local sign-out still proceeds if the server session already expired.
    }
    clearStoredAuth();
    setAuth(null);
  }

  if (isRestoringSession) {
    return null;
  }

  if (!auth) {
    return <AuthScreen api={api} onAuth={handleAuth} notice={notice} setNotice={setNotice} />;
  }

  return (
    <AppShell
      api={api}
      user={auth.user}
      onLogout={logout}
      notice={notice}
      setNotice={setNotice}
    />
  );
}
