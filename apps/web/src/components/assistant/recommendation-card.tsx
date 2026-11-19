import { Link } from "@tanstack/react-router"
import { Activity, ArrowRight, Sparkles } from "lucide-react"
import type { ProjectRecommendationCard } from "./types"
import { CitationPopover } from "./citation-popover"

interface RecommendationCardProps {
  card: ProjectRecommendationCard
  className?: string
}

function getMatchBadgeStyle(level: string): string {
  const normalized = level.toLowerCase()
  if (normalized.includes("strong") || normalized.includes("high")) {
    return "border-emerald-500/30 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400"
  }
  if (
    normalized.includes("possible") ||
    normalized.includes("moderate") ||
    normalized.includes("medium")
  ) {
    return "border-amber-500/30 bg-amber-500/10 text-amber-600 dark:text-amber-400"
  }
  return "border-sky-500/30 bg-sky-500/10 text-sky-600 dark:text-sky-400"
}

export function RecommendationCard({ card, className = "" }: RecommendationCardProps) {
  const matchStyle = getMatchBadgeStyle(card.matchLevel || "Possible match")

  return (
    <div
      className={`rounded-xl border border-border/80 bg-card p-3 shadow-xs space-y-2.5 text-left text-card-foreground transition-all hover:border-primary/40 ${className}`}
    >
      {/* Header: Title and Badges */}
      <div className="flex flex-col gap-1.5">
        <div className="flex items-start justify-between gap-2">
          <Link
            to="/projects/$projectId"
            params={{ projectId: card.projectId }}
            className="font-semibold text-xs sm:text-sm text-foreground hover:text-primary transition-colors line-clamp-1"
            title={card.projectName}
          >
            {card.projectName}
          </Link>
          <span
            className={`shrink-0 rounded-full border px-2 py-0.5 text-[10px] font-semibold tracking-tight ${matchStyle}`}
          >
            {card.matchLevel}
          </span>
        </div>

        {/* Category & Technologies */}
        <div className="flex flex-wrap items-center gap-1 text-[10px]">
          {card.category && (
            <span className="rounded bg-muted px-1.5 py-0.5 font-medium text-muted-foreground border border-border/50">
              {card.category}
            </span>
          )}
          {card.technologies &&
            card.technologies.slice(0, 4).map((tech) => (
              <span
                key={tech}
                className="rounded bg-secondary/80 px-1.5 py-0.5 font-mono text-[10px] text-secondary-foreground border border-border/30"
              >
                {tech}
              </span>
            ))}
          {card.technologies && card.technologies.length > 4 && (
            <span className="text-[10px] text-muted-foreground font-mono">
              +{card.technologies.length - 4}
            </span>
          )}
        </div>
      </div>

      {/* Why it matches */}
      {card.whyItMatches && (
        <div className="space-y-0.5">
          <div className="flex items-center gap-1 text-[11px] font-semibold text-foreground">
            <Sparkles className="h-3 w-3 text-primary shrink-0" />
            <span>Why it matches</span>
          </div>
          <p className="text-xs text-muted-foreground leading-relaxed">
            {card.whyItMatches}
          </p>
        </div>
      )}

      {/* Activity Summary */}
      {card.activitySummary && (
        <div className="flex items-center gap-1.5 text-[11px] text-muted-foreground">
          <Activity className="h-3 w-3 text-emerald-500 shrink-0" />
          <span className="truncate">{card.activitySummary}</span>
        </div>
      )}

      {/* Citations / Evidence expander */}
      <CitationPopover citations={card.citations} evidence={card.evidence} />

      {/* Action CTA */}
      <Link
        to="/projects/$projectId"
        params={{ projectId: card.projectId }}
        className="mt-2 flex items-center justify-center gap-1.5 w-full rounded-md bg-primary px-3 py-1.5 text-xs font-semibold text-primary-foreground hover:opacity-90 transition-opacity"
      >
        <span>Fund with OSSFuel</span>
        <ArrowRight className="h-3.5 w-3.5" />
      </Link>
    </div>
  )
}
