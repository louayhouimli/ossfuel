import { eyebrowSection } from "@/lib/styles"
import { cn } from "@workspace/ui/lib/utils"

type PageHeaderProps = {
  eyebrow: string
  title: string
  description?: string
  className?: string
}

/**
 * The eyebrow, title and summary that open every app screen.
 *
 * Reason: five pages repeated this block with drifted spacing and type sizes,
 * so the screen titles no longer matched each other. `break-words` is on the
 * title because project and repository names can be longer than a phone is
 * wide, which was pushing the page into horizontal scroll.
 */
export function PageHeader({
  eyebrow,
  title,
  description,
  className,
}: PageHeaderProps) {
  return (
    <div className={cn(className)}>
      <p className={eyebrowSection}>{eyebrow}</p>
      <h1 className="mt-4 font-sans text-3xl leading-tight font-bold tracking-[-0.04em] break-words sm:text-4xl">
        {title}
      </h1>
      {description ? (
        <p className="mt-4 max-w-2xl leading-7 text-muted-foreground">
          {description}
        </p>
      ) : null}
    </div>
  )
}
