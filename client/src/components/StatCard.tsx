import type { Icon } from "@phosphor-icons/react";

interface StatCardProps {
  label: string;
  value: number | string;
  tone?: "neutral" | "braking" | "cornering" | "acceleration";
  icon: Icon;
}

export function StatCard({ label, value, tone = "neutral", icon: IconComponent }: StatCardProps) {
  return (
    <div className={`stat-card stat-card--${tone}`}>
      <div className="stat-card__label">
        <IconComponent size={13} weight="bold" />
        {label}
      </div>
      <div className="stat-card__value">{value}</div>
    </div>
  );
}
