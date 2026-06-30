const legacyTokenStorageKey = "stokio.accessToken";
const legacyUserStorageKey = "stokio.user";
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

export function clearStoredAuth() {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.removeItem(legacyTokenStorageKey);
  window.localStorage.removeItem(legacyUserStorageKey);
  window.sessionStorage.removeItem(sessionStorageKey);
}
