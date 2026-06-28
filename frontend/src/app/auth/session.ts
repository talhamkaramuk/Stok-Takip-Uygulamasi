export const legacyTokenStorageKey = "stokio.accessToken";
export const legacyUserStorageKey = "stokio.user";

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
