import { cn } from "@workspace/ui/lib/utils"

export type ChatbotIconVariant = "buddy" | "walker" | "wave" | "stance" | "accent"

type ChatbotIconProps = {
  size?: number
  variant?: ChatbotIconVariant
  className?: string
}

const BODY_PATH =
  "M53.06 23.44L58 32L45 54.52L19 54.52L6 32L19 9.48L45 9.48L48.25 15.11L42.48 21.11L40.38 17.48L23.62 17.48L15.24 32L23.62 46.52L40.38 46.52L48.76 32L45.58 26.48ZM47.85 17.56L30.03 29.11L33.97 34.89L51.23 22.52ZM25 32A7 7 0 0 1 39 32A7 7 0 0 1 25 32Z"

export function ChatbotIcon({
  size = 32,
  variant = "buddy",
  className,
}: ChatbotIconProps) {
  return (
    <svg
      viewBox="0 0 64 64"
      width={size}
      height={size}
      fill="currentColor"
      aria-hidden="true"
      focusable="false"
      className={cn("shrink-0", className)}
    >
      {variant === "walker" && (
        <g>
          {/* Top Antenna */}
          <path d="M31 1.5H33V7H31Z" />
          <circle cx="32" cy="2" r="2.5" />
          {/* Main Chassis */}
          <g transform="translate(32, 25) scale(0.82) translate(-32, -32)">
            <path fillRule="evenodd" clipRule="evenodd" d={BODY_PATH} />
          </g>
          {/* Articulated Tech Legs */}
          <path d="M22 43.5 L18 51 L18 58 L13 60 L22 60 L22 56 L21 51 L24.5 43.5 Z" />
          <path d="M39.5 43.5 L43 51 L42 56 L42 60 L51 60 L46 58 L46 51 L42 43.5 Z" />
        </g>
      )}

      {variant === "buddy" && (
        <g>
          {/* Little Top Antenna */}
          <rect x="30.5" y="2" width="3" height="6" rx="1.5" />
          <circle cx="32" cy="2.5" r="2.5" />
          {/* Arms */}
          <rect
            x="6.5"
            y="26"
            width="3.5"
            height="9"
            rx="1.75"
            transform="rotate(15 8.25 30.5)"
          />
          <rect
            x="54"
            y="26"
            width="3.5"
            height="9"
            rx="1.75"
            transform="rotate(-15 55.75 30.5)"
          />
          {/* Main Chassis */}
          <g transform="translate(32, 25) scale(0.80) translate(-32, -32)">
            <path fillRule="evenodd" clipRule="evenodd" d={BODY_PATH} />
          </g>
          {/* Cute Robot Legs & Feet */}
          <rect x="20" y="42" width="4.5" height="13" rx="2.25" />
          <path d="M16 57 C16 55 18 53.5 22 53.5 C25 53.5 26.5 55 26.5 57 L26.5 59.5 C26.5 60.5 25.5 61 24.5 61 L18 61 C16.5 61 16 60 16 59.5 Z" />
          <rect x="39.5" y="42" width="4.5" height="13" rx="2.25" />
          <path d="M37.5 57 C37.5 55 39 53.5 42 53.5 C46 53.5 48 55 48 57 L48 59.5 C48 60.5 47.5 61 46 61 L39.5 61 C38.5 61 37.5 60.5 37.5 59.5 Z" />
          {/* Eyes in core */}
          <ellipse cx="30" cy="25" rx="1.2" ry="2" className="fill-background" />
          <ellipse cx="34" cy="25" rx="1.2" ry="2" className="fill-background" />
        </g>
      )}

      {variant === "wave" && (
        <g>
          {/* Headset Antenna */}
          <path
            d="M22 17 C22 11 26 8 32 8 C38 8 42 11 42 17"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.5"
            strokeLinecap="round"
          />
          <circle cx="21" cy="18" r="2.5" />
          <circle cx="43" cy="18" r="2.5" />
          <path
            d="M43 18 L46 25 L43 25"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
          <circle cx="43" cy="25" r="1.5" />
          {/* Main Chassis */}
          <g transform="translate(32, 27) scale(0.78) translate(-32, -32)">
            <path fillRule="evenodd" clipRule="evenodd" d={BODY_PATH} />
          </g>
          {/* Waving Arm */}
          <path d="M10 28 C7 24 6 18 10 14 C12 12 14 14 13 17 L13 25 Z" />
          <circle cx="10" cy="13" r="2" />
          {/* Right Arm */}
          <rect
            x="54"
            y="27"
            width="3.5"
            height="9"
            rx="1.75"
            transform="rotate(-10 55.75 31.5)"
          />
          {/* Legs */}
          <rect x="22" y="44" width="4" height="12" rx="2" />
          <path d="M18 57 C18 55 20 54 23 54 C27 54 28 55 28 57 L28 60 C28 60.8 27.2 61 26 61 L20 61 C18.8 61 18 60.8 18 60 Z" />
          <rect x="38" y="44" width="4" height="12" rx="2" />
          <path d="M36 57 C36 55 37 54 41 54 C44 54 46 55 46 57 L46 60 C46 60.8 45.2 61 44 61 L38 61 C36.8 61 36 60.8 36 60 Z" />
        </g>
      )}

      {variant === "stance" && (
        <g>
          <path fillRule="evenodd" clipRule="evenodd" d={BODY_PATH} />
          {/* Drop-in Stance Feet */}
          <path d="M20 52 L17 59.5 L11 59.5 C10 59.5 9.5 60.5 9.5 61.5 L9.5 62.5 C9.5 63.5 10.5 64 11.5 64 L25 64 C25.8 64 26.5 63.5 26.5 62.5 L26.5 60 L24 53 Z" />
          <path d="M44 52 L40 53 L37.5 60 L37.5 62.5 C37.5 63.5 38.2 64 39 64 L52.5 64 C53.5 64 54.5 63.5 54.5 62.5 L54.5 61.5 C54.5 60.5 54 59.5 53 59.5 L47 59.5 L44 52 Z" />
          {/* Mini Beacon */}
          <path d="M30.5 4 L33.5 4 L33 9.48 L31 9.48 Z" />
          <circle cx="32" cy="3.5" r="2.5" />
        </g>
      )}

      {variant === "accent" && (
        <g>
          <rect x="31" y="4" width="2" height="6" rx="1" />
          <rect x="21" y="43" width="4.5" height="12" rx="2.25" />
          <path d="M16 57 C16 55 18 54 22 54 C26 54 27 55 27 57 L27 60 C27 60.8 26.2 61 25 61 L17 61 C16.2 61 16 60.8 16 60 Z" />
          <rect x="38.5" y="43" width="4.5" height="12" rx="2.25" />
          <path d="M37 57 C37 55 38 54 42 54 C46 54 48 55 48 57 L48 60 C48 60.8 47.2 61 46 61 L38 61 C37.2 61 37 60.8 37 60 Z" />
          <rect x="8" y="27" width="3.5" height="8" rx="1.75" />
          <rect x="52.5" y="27" width="3.5" height="8" rx="1.75" />
          <g transform="translate(32, 26) scale(0.80) translate(-32, -32)">
            <path fillRule="evenodd" clipRule="evenodd" d={BODY_PATH} />
          </g>
          {/* Fuel Orange Accents */}
          <circle cx="32" cy="3.5" r="2.5" className="fill-[#FF5E36]" />
          <circle cx="30" cy="26" r="1.5" className="fill-[#FF5E36]" />
          <circle cx="34" cy="26" r="1.5" className="fill-[#FF5E36]" />
        </g>
      )}
    </svg>
  )
}

export default ChatbotIcon
