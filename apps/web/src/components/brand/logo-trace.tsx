import { cn } from "@workspace/ui/lib/utils"

/**
 * The mark's own edges, re-drawn as stroked paths so a dash can travel them.
 *
 * The numbers are the logo's vertices: `LogoMark` fills this shape as one
 * `evenodd` path, which cannot be drawn progressively.
 */
const outerRing = "M58 32 L45 54.52 L19 54.52 L6 32 L19 9.48 L45 9.48 Z"
const innerRing =
  "M48.76 32 L40.38 46.52 L23.62 46.52 L15.24 32 L23.62 17.48 L40.38 17.48 Z"
const slash = "M47.85 17.56 L30.03 29.11 L33.97 34.89 L51.23 22.52 Z"
const core = "M25 32 A7 7 0 0 1 39 32 A7 7 0 0 1 25 32 Z"

/** The order the flow section reads: ring, balance, route, record. */
const strokes = [
  {
    d: outerRing,
    className:
      "animate-[logoTrace_1.2s_ease-out_100ms_both,logoTraceHead_1.2s_ease-out_100ms_both] motion-reduce:animate-none",
  },
  {
    d: innerRing,
    className:
      "animate-[logoTrace_1s_ease-out_700ms_both,logoTraceHead_1s_ease-out_700ms_both] motion-reduce:animate-none",
  },
  {
    d: slash,
    className:
      "animate-[logoTrace_450ms_ease-out_1.5s_both,logoTraceHead_450ms_ease-out_1.5s_both] motion-reduce:animate-none",
  },
  {
    d: core,
    className:
      "animate-[logoTrace_500ms_ease-out_1.85s_both,logoTraceHead_500ms_ease-out_1.85s_both] motion-reduce:animate-none",
  },
]

type LogoTraceProps = {
  className?: string
}

/**
 * The OSSFuel mark drawn in hairlines, for the hero.
 *
 * It is the mark rather than an abstract pattern because a stock grid or glow
 * would say nothing about the product, while this outlines what the four flow
 * steps describe. `non-scaling-stroke` keeps it hairlines at any size, so it
 * stays at the site's border weight rather than becoming a slab.
 */
export function LogoTrace({ className }: LogoTraceProps) {
  return (
    <svg
      viewBox="0 0 64 64"
      aria-hidden="true"
      focusable="false"
      className={cn("text-foreground/10", className)}
    >
      <g
        fill="none"
        stroke="currentColor"
        strokeWidth={1.5}
        strokeDasharray={1}
        strokeDashoffset={0}
      >
        {strokes.map((stroke) => (
          <path
            key={stroke.d}
            d={stroke.d}
            pathLength={1}
            vectorEffect="non-scaling-stroke"
            className={stroke.className}
          />
        ))}
      </g>
    </svg>
  )
}
