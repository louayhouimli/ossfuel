import type { FundingStatus } from "@/lib/funding"
import { cn } from "@workspace/ui/lib/utils"

const labels: Record<FundingStatus, string> = {
  pending: "Pending",
  succeeded: "Succeeded",
  failed: "Failed",
  cancelled: "Cancelled",
}

const styles: Record<FundingStatus, string> = {
  pending: "border-foreground/30 text-muted-foreground",
  succeeded: "border-primary bg-primary text-primary-foreground",
  failed: "border-destructive/50 text-destructive",
  cancelled: "border-foreground/30 text-muted-foreground",
}

export function FundingStatusBadge({
  status,
  className,
}: {
  status: FundingStatus
  className?: string
}) {
  return (
    <span
      className={cn(
        "inline-flex border px-2.5 py-1 text-xs font-semibold tracking-[0.12em] uppercase",
        styles[status],
        className
      )}
    >
      {labels[status]}
    </span>
  )
}
