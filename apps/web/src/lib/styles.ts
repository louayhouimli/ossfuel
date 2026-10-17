import { cn } from "@workspace/ui/lib/utils"

/**
 * The single source for the class strings the app pages repeat.
 *
 * These live here instead of in `packages/ui` because they are this site's
 * vocabulary (square corners, ink-on-paper, hairline borders), not shadcn
 * primitives. The shadcn `Button` still exists for the auth screens, which
 * use its own rounded, softer register.
 */

const focusRing =
  "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"

/** Page-level action, 48px tall. */
export const buttonPrimary = cn(
  "inline-flex min-h-12 items-center justify-center gap-2 rounded-sm bg-primary px-6 text-sm font-semibold text-primary-foreground",
  focusRing
)

/** Page-level secondary action, 48px tall. */
export const buttonOutline = cn(
  "inline-flex min-h-12 items-center justify-center gap-2 rounded-sm border border-foreground/20 px-6 text-sm font-semibold",
  focusRing
)

/** In-row action, 44px tall. */
export const buttonPrimaryCompact = cn(
  "inline-flex min-h-11 items-center justify-center gap-2 rounded-sm bg-primary px-5 text-sm font-semibold text-primary-foreground",
  focusRing
)

/** In-row secondary action, 44px tall. */
export const buttonOutlineCompact = cn(
  "inline-flex min-h-11 items-center justify-center gap-2 rounded-sm border border-foreground/20 px-5 text-sm font-semibold",
  focusRing
)

/** Bordered, subtly filled surface: empty states, stat tiles, list rows. */
export const panel = "border border-foreground/20 bg-secondary p-5 sm:p-6"

/** The label above a page title. */
export const eyebrowSection =
  "text-sm font-semibold tracking-[0.18em] text-muted-foreground uppercase"

/** The label above a card or tile heading. */
export const eyebrowItem =
  "text-xs font-semibold tracking-[0.16em] text-muted-foreground uppercase"
