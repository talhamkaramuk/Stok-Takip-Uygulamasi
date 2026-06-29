import { useEffect, useMemo, useState } from "react";
import { createApiClient } from "../shared/api/client";
import type { Notice } from "../shared/types/ui";
import type { AuthResponse } from "../types";
import { AppShell } from "./AppShell";
import { AuthScreen } from "./auth/AuthScreen";
import {
  clearStoredAuth,
  legacyTokenStorageKey,
  legacyUserStorageKey,
  readStoredAuth,
  saveStoredAuth
} from "./auth/session";

export default function StokioApp() {
  const [auth, setAuth] = useState<AuthResponse | null>(() => readStoredAuth());
  const [notice, setNotice] = useState<Notice | null>(null);
  const api = useMemo(() => createApiClient(auth?.accessToken ?? null), [auth?.accessToken]);

  useEffect(() => {
    localStorage.removeItem(legacyTokenStorageKey);
    localStorage.removeItem(legacyUserStorageKey);
  }, []);

  useEffect(() => {
    if (!auth) {
      return undefined;
    }

    const expiresInMs = Date.parse(auth.expiresAt) - Date.now();
    if (!Number.isFinite(expiresInMs) || expiresInMs <= 0) {
      clearStoredAuth();
      setAuth(null);
      return undefined;
    }

    const timeout = window.setTimeout(() => {
      clearStoredAuth();
      setAuth(null);
    }, Math.min(expiresInMs, 2_147_483_647));

    return () => {
      window.clearTimeout(timeout);
    };
  }, [auth]);

  function handleAuth(response: AuthResponse) {
    setNotice(null);
    saveStoredAuth(response);
    setAuth(response);
  }

  function logout() {
    setNotice(null);
    clearStoredAuth();
    setAuth(null);
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
