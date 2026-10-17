import { LogoMark } from "@/components/brand/logo"
import { cn } from "@workspace/ui/lib/utils"

type LoaderProps = {
  /** Names what is being fetched. Announced to screen readers. */
  label: string
  size?: number
  className?: string
}

/**
 * Wait state for anything the app fetches.
 *
 * The mark spins because the app is busy, not for decoration, and it is the
 * same mark as the header so a slow request still looks like OSSFuel. The
 * label carries the meaning (the mark is aria-hidden), and the spin is dropped
 * under prefers-reduced-motion.
 */
export function Loader({ label, size = 20, className }: LoaderProps) {
  return (
    <span
      role="status"
      className={cn(
        "inline-flex items-center gap-3 text-muted-foreground",
        className
      )}
    >
      <LogoMark
        size={size}
        className="shrink-0 animate-spin [animation-duration:1.4s] motion-reduce:animate-none"
      />
      <span className="text-sm leading-6">{label}</span>
    </span>
  )
}
