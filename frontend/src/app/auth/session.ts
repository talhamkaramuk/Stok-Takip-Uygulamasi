import type { AuthResponse } from "../../types";

export const legacyTokenStorageKey = "stokio.accessToken";
export const legacyUserStorageKey = "stokio.user";
const sessionStorageKey = "stokio.session";

const demoCredentialsEnabled = import.meta.env.DEV || import.meta.env.VITE_ENABLE_DEMO_CREDENTIALS === "true";
export const demoPassword = demoCredentialsEnabled ? "StrongPass123" : "";

export const initialAuthForm = demoCredentialsEnabled
  ? {
      businessName: "STOKIO Demo",
      tenantSlug: "stokio-demo",
      ownerName: "Talha",
      email: "owner@stokio.local",
      password: demoPassword
    }
  : {
      businessName: "",
      tenantSlug: "",
      ownerName: "",
      email: "",
      password: ""
    };

export function readStoredAuth(): AuthResponse | null {
  if (typeof window === "undefined") {
    return null;
  }

  try {
    const rawValue = window.sessionStorage.getItem(sessionStorageKey);
    if (!rawValue) {
      return null;
    }

    const value = JSON.parse(rawValue) as AuthResponse;
    if (!value.accessToken || !value.expiresAt || !value.user) {
      clearStoredAuth();
      return null;
    }

    const expiresAt = Date.parse(value.expiresAt);
    if (!Number.isFinite(expiresAt) || expiresAt <= Date.now() + 30_000) {
      clearStoredAuth();
      return null;
    }

    return value;
  } catch {
    clearStoredAuth();
    return null;
  }
}

export function saveStoredAuth(response: AuthResponse) {
  if (typeof window === "undefined") {
    return;
  }

  window.sessionStorage.setItem(sessionStorageKey, JSON.stringify(response));
}

export function clearStoredAuth() {
  if (typeof window === "undefined") {
    return;
  }

  window.sessionStorage.removeItem(sessionStorageKey);
}
