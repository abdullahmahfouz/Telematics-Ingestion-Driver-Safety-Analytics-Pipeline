import { WarningCircle } from "@phosphor-icons/react";

interface ErrorBannerProps {
  message: string;
}

export function ErrorBanner({ message }: ErrorBannerProps) {
  return (
    <div className="banner banner--error">
      <WarningCircle size={16} weight="bold" />
      {message}
    </div>
  );
}
