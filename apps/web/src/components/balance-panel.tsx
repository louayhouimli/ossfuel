import { useState } from "react"
import { useQuery } from "@tanstack/react-query"
import { ArrowLeft, ArrowRight } from "lucide-react"
import { Loader } from "@/components/loader"
import {
  balanceQueryOptions,
  balanceTransactionsQueryOptions,
  formatMoney,
} from "@/lib/funding"
import { buttonOutlineCompact, eyebrowItem, panel } from "@/lib/styles"
import { cn } from "@workspace/ui/lib/utils"

const PAGE_SIZE = 10

const timestampFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
})

type BalancePanelProps = {
  projectId: string
}

/**
 * The project's AI Balance and every credit that built it.
 *
 * Both are public on purpose: a sponsor should be able to check where a payment
 * went, and a maintainer should never have to explain a balance in private.
 * Sponsor identity lives only in the sponsor's own funding history.
 */
export function BalancePanel({ projectId }: BalancePanelProps) {
  const [page, setPage] = useState(1)
  const balance = useQuery(balanceQueryOptions(projectId))
  const history = useQuery(
    balanceTransactionsQueryOptions(projectId, page, PAGE_SIZE)
  )

  const totalPages = history.data
    ? Math.max(1, Math.ceil(history.data.totalCount / history.data.pageSize))
    : 1

  return (
    <section className={panel} aria-labelledby="balance-heading">
      <p className={eyebrowItem}>AI Balance</p>
      <h2 id="balance-heading" className="sr-only">
        AI Balance and funding history
      </h2>

      {balance.isLoading ? (
        <Loader label="Loading the balance" className="mt-4" />
      ) : null}

      {balance.isError ? (
        <p className="mt-4 text-sm text-muted-foreground" role="alert">
          The balance could not be loaded. Try again shortly.
        </p>
      ) : null}

      {balance.data ? (
        <>
          <p className="mt-3 font-sans text-4xl font-bold tracking-[-0.05em] break-words">
            {formatMoney(balance.data.amountMinor, balance.data.currency)}
          </p>
          <p className="mt-2 text-sm text-muted-foreground">
            {balance.data.currency.toUpperCase()} · public project balance
          </p>
        </>
      ) : null}

      <div className="mt-8 border-t border-foreground/15 pt-6">
        <h3 className={eyebrowItem}>Funding history</h3>

        {history.isLoading ? (
          <Loader label="Loading funding history" className="mt-4" />
        ) : null}

        {history.isError ? (
          <p className="mt-4 text-sm text-muted-foreground" role="alert">
            The funding history could not be loaded. Try again shortly.
          </p>
        ) : null}

        {history.data && history.data.items.length === 0 ? (
          <p className="mt-4 max-w-lg text-sm leading-6 text-muted-foreground">
            No funding has arrived yet. The first sponsor payment opens the
            history.
          </p>
        ) : null}

        {history.data && history.data.items.length > 0 ? (
          <>
            <ol
              aria-busy={history.isFetching}
              className={cn("mt-4", history.isFetching && "opacity-60")}
            >
              {history.data.items.map((transaction) => (
                <li
                  key={transaction.id}
                  className="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-1 border-b border-foreground/10 py-3 first:pt-0 last:border-b-0"
                >
                  <div className="min-w-0">
                    <p className="text-sm font-semibold">
                      {transaction.description ??
                        transactionLabel(transaction.type)}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      <time dateTime={transaction.createdAt}>
                        {timestampFormat.format(
                          new Date(transaction.createdAt)
                        )}
                      </time>
                    </p>
                  </div>
                  <p className="text-sm font-semibold">
                    {formatMoney(transaction.amountMinor)}
                  </p>
                </li>
              ))}
            </ol>

            {totalPages > 1 ? (
              <nav
                className="mt-5 flex flex-wrap items-center justify-between gap-3"
                aria-label="Funding history pages"
              >
                <p className="text-sm text-muted-foreground" aria-live="polite">
                  Page {page} of {totalPages}
                </p>
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={() =>
                      setPage((current) => Math.max(1, current - 1))
                    }
                    disabled={page <= 1}
                    className={cn(
                      buttonOutlineCompact,
                      "px-4 disabled:opacity-40"
                    )}
                  >
                    <ArrowLeft size={16} aria-hidden="true" />
                    Previous
                  </button>
                  <button
                    type="button"
                    onClick={() =>
                      setPage((current) => Math.min(totalPages, current + 1))
                    }
                    disabled={page >= totalPages}
                    className={cn(
                      buttonOutlineCompact,
                      "px-4 disabled:opacity-40"
                    )}
                  >
                    Next
                    <ArrowRight size={16} aria-hidden="true" />
                  </button>
                </div>
              </nav>
            ) : null}
          </>
        ) : null}
      </div>
    </section>
  )
}

function transactionLabel(type: string) {
  return type === "funding_credit" ? "Funding credit" : type
}
