import type { ReactNode } from "react";

export function TabButton({ active, onClick, icon, label }: { active: boolean; onClick: () => void; icon: ReactNode; label: string }) {
  return (
    <button className={active ? "active" : ""} onClick={onClick} type="button">
      <span className="nav-icon">{icon}</span>
      <span>{label}</span>
    </button>
  );
}
