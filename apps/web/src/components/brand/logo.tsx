import { cn } from "@workspace/ui/lib/utils"

type LogoProps = {
  size?: number
  showWordmark?: boolean
  className?: string
}

export function Logo({
  size = 32,
  showWordmark = true,
  className,
}: LogoProps) {
  return (
    <span className={cn("inline-flex items-center gap-2.5", className)}>
      <svg
        viewBox="0 0 64 64"
        width={size}
        height={size}
        fill="currentColor"
        aria-hidden="true"
        focusable="false"
      >
        <path
          fillRule="evenodd"
          clipRule="evenodd"
          d="M53.06 23.44L58 32L45 54.52L19 54.52L6 32L19 9.48L45 9.48L48.25 15.11L42.48 21.11L40.38 17.48L23.62 17.48L15.24 32L23.62 46.52L40.38 46.52L48.76 32L45.58 26.48ZM47.85 17.56L30.03 29.11L33.97 34.89L51.23 22.52ZM25 32A7 7 0 0 1 39 32A7 7 0 0 1 25 32Z"
        />
      </svg>
      {showWordmark ? (
        <span className="text-xl leading-none font-bold tracking-[-0.017em]">
          OSSFuel
        </span>
      ) : null}
    </span>
  )
}

export default Logo