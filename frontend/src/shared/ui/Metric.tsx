import type { ReactNode } from "react";

export function Metric({ label, value, icon, tone }: { label: string; value: string; icon: ReactNode; tone?: "ok" | "warn" }) {
  return (
    <article className={`metric ${tone ?? ""}`}>
      <span>{icon}</span>
      <div>
        <strong>{value}</strong>
        <p>{label}</p>
      </div>
    </article>
  );
}
