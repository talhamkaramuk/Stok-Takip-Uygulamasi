import type { ReactNode } from "react";

export type TabKey =
  | "dashboard"
  | "products"
  | "orders"
  | "purchase"
  | "shipments"
  | "returns"
  | "categories"
  | "customers"
  | "suppliers"
  | "warehouses"
  | "stock"
  | "count"
  | "users"
  | "profile"
  | "reports";

export type Notice = {
  type: "success" | "error";
  message: string;
};

export type MetricItem = {
  label: string;
  value: string;
  icon: ReactNode;
  tone?: "ok" | "warn";
};
