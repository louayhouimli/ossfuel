import * as React from "react"
import { ChevronDown, ChevronUp, FileText, Quote } from "lucide-react"
import type { ProjectRecommendationCitation } from "./types"

interface CitationPopoverProps {
  citations?: ProjectRecommendationCitation[]
  evidence?: string
  className?: string
}

export function CitationPopover({
  citations = [],
  evidence,
  className = "",
}: CitationPopoverProps) {
  const [isOpen, setIsOpen] = React.useState(false)

  const hasCitations = citations && citations.length > 0
  const hasEvidence = Boolean(evidence?.trim())

  if (!hasCitations && !hasEvidence) {
    return null
  }

  const count = citations.length

  return (
    <div className={`mt-1.5 w-full ${className}`}>
      <button
        type="button"
        onClick={() => setIsOpen((prev) => !prev)}
        aria-expanded={isOpen}
        className="flex items-center justify-between w-full rounded-md border border-border/60 bg-muted/30 px-2 py-1 text-[11px] font-medium text-muted-foreground hover:bg-muted/60 hover:text-foreground transition-colors"
      >
        <span className="flex items-center gap-1.5">
          <Quote className="h-3 w-3 text-primary shrink-0" />
          <span>
            {hasCitations
              ? `${count} README citation${count > 1 ? "s" : ""}`
              : "View README evidence"}
          </span>
        </span>
        {isOpen ? (
          <ChevronUp className="h-3 w-3 shrink-0" />
        ) : (
          <ChevronDown className="h-3 w-3 shrink-0" />
        )}
      </button>

      {isOpen && (
        <div className="mt-1.5 space-y-2 rounded-md border border-border/80 bg-background/90 p-2 text-left shadow-xs transition-all">
          {hasCitations ? (
            citations.map((citation, index) => (
              <div
                key={citation.chunkId ? `${citation.chunkId}-${index}` : index}
                className="space-y-1 border-b border-border/40 pb-1.5 last:border-b-0 last:pb-0"
              >
                {citation.headerPath && (
                  <div className="flex items-center gap-1 font-mono text-[10px] text-muted-foreground">
                    <FileText className="h-2.5 w-2.5 text-primary shrink-0" />
                    <span className="truncate">{citation.headerPath}</span>
                  </div>
                )}
                <blockquote className="border-l-2 border-primary/60 pl-2 text-[11px] italic text-foreground/80 leading-relaxed break-words">
                  &ldquo;{citation.quote}&rdquo;
                </blockquote>
              </div>
            ))
          ) : (
            <div className="text-[11px] italic text-muted-foreground leading-relaxed break-words">
              {evidence}
            </div>
          )}
        </div>
      )}
    </div>
  )
}
