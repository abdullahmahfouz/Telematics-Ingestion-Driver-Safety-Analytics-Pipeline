import type { Icon } from "@phosphor-icons/react";

interface StatCardProps {
  label: string;
  value: number | string;
  tone?: "neutral" | "warning";
  icon: Icon;
}

export function StatCard({ label, value, tone = "neutral", icon: IconComponent }: StatCardProps) {
  return (
    <div className={`stat-card stat-card--${tone}`}>
      <div className="stat-card__icon">
        <IconComponent size={18} weight="bold" />
      </div>
      <div className="stat-card__value">{value}</div>
      <div className="stat-card__label">{label}</div>
    </div>
  );
}
