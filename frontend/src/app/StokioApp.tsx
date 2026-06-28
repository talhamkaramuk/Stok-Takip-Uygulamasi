import { useEffect, useMemo, useState } from "react";
import { createApiClient } from "../shared/api/client";
import type { Notice } from "../shared/types/ui";
import type { AuthResponse } from "../types";
import { AppShell } from "./AppShell";
import { AuthScreen } from "./auth/AuthScreen";
import { legacyTokenStorageKey, legacyUserStorageKey } from "./auth/session";

export default function StokioApp() {
  const [token, setToken] = useState<string | null>(null);
  const [user, setUser] = useState<AuthResponse["user"] | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const api = useMemo(() => createApiClient(token), [token]);

  useEffect(() => {
    localStorage.removeItem(legacyTokenStorageKey);
    localStorage.removeItem(legacyUserStorageKey);
  }, []);

  function handleAuth(response: AuthResponse) {
    setNotice(null);
    setToken(response.accessToken);
    setUser(response.user);
  }

  function logout() {
    setNotice(null);
    setToken(null);
    setUser(null);
  }

  if (!token || !user) {
    return <AuthScreen api={api} onAuth={handleAuth} notice={notice} setNotice={setNotice} />;
  }

  return (
    <AppShell
      api={api}
      user={user}
      onLogout={logout}
      notice={notice}
      setNotice={setNotice}
    />
  );
}
