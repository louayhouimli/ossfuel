import { createFileRoute, Link, useNavigate } from "@tanstack/react-router"
import { useQuery } from "@tanstack/react-query"
import { ArrowLeft, ArrowRight } from "lucide-react"
import { FundingStatusBadge } from "@/components/funding-status"
import { Loader } from "@/components/loader"
import { PageHeader } from "@/components/page-header"
import { formatMoney, fundingsQueryOptions } from "@/lib/funding"
import { buttonOutlineCompact, buttonPrimary, panel } from "@/lib/styles"
import { cn } from "@workspace/ui/lib/utils"

const PAGE_SIZE = 12

const timestampFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
})

export const Route = createFileRoute("/_site/_authenticated/fundings/")({
  validateSearch: (search: Record<string, unknown>): { page?: number } => ({
    page: Math.max(1, Number(search.page) || 1),
  }),
  head: () => ({
    meta: [
      { title: "Funding | OSSFuel" },
      {
        name: "description",
        content:
          "The payments you started for open-source projects on OSSFuel, kept private to your account.",
      },
    ],
  }),
  component: FundingsPage,
})

function FundingsPage() {
  const { page = 1 } = Route.useSearch()
  const navigate = useNavigate()
  const { data, isLoading, isFetching, isError } = useQuery(
    fundingsQueryOptions(page, PAGE_SIZE)
  )

  const totalPages = data
    ? Math.max(1, Math.ceil(data.totalCount / data.pageSize))
    : 1

  const goToPage = (next: number) => {
    void navigate({ to: "/fundings", search: { page: next } })
    window.scrollTo({ top: 0 })
  }

  return (
    <main className="mx-auto max-w-6xl px-5 py-8 sm:px-8 sm:py-12">
      <PageHeader
        eyebrow="Funding"
        title="Your funding history."
        description="Payments you started for open-source projects. This history is private to you; each project's balance and credits are public on its page."
      />

      {isLoading ? (
        <Loader label="Loading your funding history" className="mt-10" />
      ) : null}

      {isError ? (
        <p
          role="alert"
          className="mt-10 border border-destructive/40 bg-destructive/10 px-5 py-4 text-sm text-foreground"
        >
          Your funding history could not be loaded. Try again shortly.
        </p>
      ) : null}

      {data && data.items.length === 0 ? (
        <div className={cn(panel, "mt-10")}>
          <h2 className="font-sans text-2xl font-bold tracking-[-0.04em]">
            You have not funded a project yet.
          </h2>
          <p className="mt-3 max-w-lg leading-7 text-muted-foreground">
            Pick a project, choose an amount, and the payment will show up here.
          </p>
          <Link to="/projects" className={cn(buttonPrimary, "mt-6 w-fit")}>
            Browse projects
          </Link>
        </div>
      ) : null}

      {data && data.items.length > 0 ? (
        <>
          <ul
            aria-busy={isFetching}
            className={cn(
              "mt-10 border-t border-foreground/15",
              isFetching && "opacity-60"
            )}
          >
            {data.items.map((funding) => (
              <li key={funding.id} className="border-b border-foreground/15">
                <Link
                  to="/fundings/$fundingId"
                  params={{ fundingId: funding.id }}
                  className="flex flex-col gap-4 py-5 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring sm:flex-row sm:items-center sm:justify-between"
                >
                  <div className="min-w-0">
                    <p className="text-lg font-bold tracking-[-0.03em] break-words">
                      {funding.projectFullName}
                    </p>
                    <p className="mt-1 text-sm text-muted-foreground">
                      <time dateTime={funding.createdAt}>
                        {timestampFormat.format(new Date(funding.createdAt))}
                      </time>
                    </p>
                  </div>

                  <div className="flex shrink-0 items-center gap-4">
                    <FundingStatusBadge status={funding.status} />
                    <p className="text-base font-semibold">
                      {formatMoney(funding.amountMinor, funding.currency)}
                    </p>
                  </div>
                </Link>
              </li>
            ))}
          </ul>

          {totalPages > 1 ? (
            <nav
              className="mt-8 flex flex-wrap items-center justify-between gap-4 border-t border-foreground/15 pt-6"
              aria-label="Funding pages"
            >
              <p className="text-sm text-muted-foreground" aria-live="polite">
                Page {page} of {totalPages}
              </p>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => goToPage(page - 1)}
                  disabled={page <= 1}
                  className={cn(buttonOutlineCompact, "disabled:opacity-40")}
                >
                  <ArrowLeft size={16} aria-hidden="true" />
                  Previous
                </button>
                <button
                  type="button"
                  onClick={() => goToPage(page + 1)}
                  disabled={page >= totalPages}
                  className={cn(buttonOutlineCompact, "disabled:opacity-40")}
                >
                  Next
                  <ArrowRight size={16} aria-hidden="true" />
                </button>
              </div>
            </nav>
          ) : null}
        </>
      ) : null}
    </main>
  )
}
