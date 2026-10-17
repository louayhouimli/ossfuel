import { useEffect, useState } from "react"

const absoluteFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
})

type LiveTimeProps = {
  value: string
  className?: string
}

/**
 * A timestamp that keeps moving.
 *
 * A project's repository information is captured once at import, so the
 * retrieved moment silently ages with nothing on screen to say so. This shows
 * the exact moment and ticks the age beside it, which makes a stale snapshot
 * obvious without a refresh.
 *
 * Client-only by construction: this app renders in the browser, so the first
 * render and every tick happen after mount and there is no server value to
 * disagree with.
 */
export function LiveTime({ value, className }: LiveTimeProps) {
  const [now, setNow] = useState(() => Date.now())
  const timestamp = new Date(value)

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000)

    return () => window.clearInterval(timer)
  }, [])

  return (
    <span className={className}>
      <time dateTime={value}>{absoluteFormat.format(timestamp)}</time>
      <span className="font-normal text-muted-foreground">
        {" · "}
        {formatAge(now - timestamp.getTime())}
      </span>
    </span>
  )
}

function formatAge(elapsedMilliseconds: number): string {
  const totalSeconds = Math.max(0, Math.floor(elapsedMilliseconds / 1000))

  if (totalSeconds < 5) {
    return "just now"
  }

  const totalMinutes = Math.floor(totalSeconds / 60)
  const totalHours = Math.floor(totalMinutes / 60)
  const totalDays = Math.floor(totalHours / 24)

  if (totalMinutes < 1) {
    return `${totalSeconds}s ago`
  }

  if (totalHours < 1) {
    return `${totalMinutes}m ${totalSeconds % 60}s ago`
  }

  if (totalDays < 1) {
    return `${totalHours}h ${totalMinutes % 60}m ago`
  }

  return `${totalDays}d ${totalHours % 24}h ago`
}
