import { createFileRoute, Link } from "@tanstack/react-router"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { ArrowLeft, Info, Loader2 } from "lucide-react"
import { FundingStatusBadge } from "@/components/funding-status"
import { Loader } from "@/components/loader"
import { PageHeader } from "@/components/page-header"
import { ApiError } from "@/lib/api"
import {
  cancelFunding,
  formatMoney,
  fundingQueryOptions,
  isSafeCheckoutUrl,
} from "@/lib/funding"
import { buttonPrimary, buttonOutline, eyebrowItem, panel } from "@/lib/styles"
import { cn } from "@workspace/ui/lib/utils"

const timestampFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
})

type FundingSearchParams = {
  session_id?: string
  cancelled?: boolean
}

export const Route = createFileRoute(
  "/_site/_authenticated/fundings/$fundingId"
)({
  validateSearch: (search: Record<string, unknown>): FundingSearchParams => ({
    session_id:
      typeof search.session_id === "string" && search.session_id.length > 0
        ? search.session_id
        : undefined,
    cancelled:
      search.cancelled === true || search.cancelled === "true"
        ? true
        : undefined,
  }),
  head: () => ({
    meta: [
      { title: "Funding | OSSFuel" },
      {
        name: "description",
        content:
          "The status of a payment you started for an open-source project on OSSFuel.",
      },
    ],
  }),
  component: FundingPage,
})

function FundingPage() {
  const { fundingId } = Route.useParams()
  const { session_id, cancelled } = Route.useSearch()
  const queryClient = useQueryClient()
  const {
    data: funding,
    isLoading,
    isError,
    error,
  } = useQuery({
    ...fundingQueryOptions(fundingId),
    refetchInterval: (query) =>
      query.state.data?.status === "pending" ? 2000 : false,
  })

  const cancel = useMutation({
    mutationFn: () => cancelFunding(fundingId),
    onSuccess: (updated) => {
      queryClient.setQueryData(fundingQueryOptions(fundingId).queryKey, updated)
      void queryClient.invalidateQueries({ queryKey: ["fundings"] })
    },
  })

  const notFound = isError && error instanceof ApiError && error.status === 404

  return (
    <main className="mx-auto max-w-6xl px-5 py-8 sm:px-8 sm:py-12">
      <Link
        to="/fundings"
        className="inline-flex min-h-11 items-center gap-2 rounded-sm text-sm font-semibold text-muted-foreground hover:text-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        <ArrowLeft size={16} aria-hidden="true" />
        Your funding history
      </Link>

      {isLoading ? (
        <Loader label="Loading the funding" className="mt-8" />
      ) : null}

      {notFound ? (
        <div className={cn(panel, "mt-8")}>
          <h1 className="text-2xl font-bold tracking-[-0.03em]">
            Funding not found
          </h1>
          <p className="mt-3 text-muted-foreground">
            This funding does not exist or belongs to another sponsor.
          </p>
        </div>
      ) : null}

      {isError && !notFound ? (
        <p className="mt-8 text-muted-foreground" role="alert">
          The funding could not be loaded. Try again shortly.
        </p>
      ) : null}

      {funding ? (
        <article className="mt-5">
          <PageHeader
            eyebrow="Funding"
            title={funding.projectFullName}
            description={`This payment is ${statusSentence(funding.status)}`}
          />

          {session_id && funding.status === "pending" ? (
            <div
              role="status"
              aria-live="polite"
              className={cn(
                panel,
                "mt-6 flex items-center gap-3 border-foreground/30 bg-muted/40 py-4 text-sm font-medium text-foreground"
              )}
            >
              <Loader2
                size={18}
                className="shrink-0 animate-spin text-muted-foreground"
                aria-hidden="true"
              />
              <span>
                Payment received by Stripe. Synchronizing your project&apos;s
                balance...
              </span>
            </div>
          ) : null}

          {cancelled ? (
            <div
              role="status"
              aria-live="polite"
              className={cn(
                panel,
                "mt-6 flex items-center gap-3 border-foreground/30 bg-muted/40 py-4 text-sm font-medium text-foreground"
              )}
            >
              <Info
                size={18}
                className="shrink-0 text-muted-foreground"
                aria-hidden="true"
              />
              <span>
                Payment checkout was cancelled. You can try again at any time.
              </span>
            </div>
          ) : null}

          <div className="mt-8 grid items-start gap-3 lg:grid-cols-[1.4fr_1fr]">
            <section className={cn(panel, "mt-0")}>
              <p className={eyebrowItem}>Amount</p>
              <p className="mt-3 font-sans text-4xl font-bold tracking-[-0.05em]">
                {formatMoney(funding.amountMinor, funding.currency)}
              </p>

              <dl className="mt-8 grid gap-x-6 gap-y-6 border-t border-foreground/15 pt-6 sm:grid-cols-2">
                <div>
                  <dt className={eyebrowItem}>Status</dt>
                  <dd className="mt-2">
                    <FundingStatusBadge status={funding.status} />
                  </dd>
                </div>
                <div>
                  <dt className={eyebrowItem}>Project</dt>
                  <dd className="mt-2">
                    <Link
                      to="/projects/$projectId"
                      params={{ projectId: funding.projectId }}
                      className="font-semibold break-words underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
                    >
                      {funding.projectFullName}
                    </Link>
                  </dd>
                </div>
                <div>
                  <dt className={eyebrowItem}>Started</dt>
                  <dd className="mt-2 font-semibold">
                    <time dateTime={funding.createdAt}>
                      {timestampFormat.format(new Date(funding.createdAt))}
                    </time>
                  </dd>
                </div>
                <div>
                  <dt className={eyebrowItem}>Completed</dt>
                  <dd className="mt-2 font-semibold">
                    {funding.completedAt ? (
                      <time dateTime={funding.completedAt}>
                        {timestampFormat.format(new Date(funding.completedAt))}
                      </time>
                    ) : (
                      "Not yet"
                    )}
                  </dd>
                </div>
              </dl>
            </section>

            <section className={panel}>
              {funding.status === "pending" ? (
                <>
                  <p className={eyebrowItem}>Next step</p>
                  <h2 className="mt-3 text-xl font-bold tracking-[-0.03em]">
                    Finish the payment
                  </h2>
                  <p className="mt-3 text-sm leading-6 text-muted-foreground">
                    The balance changes only after the payment completes. This
                    build uses a test checkout, so no card is charged.
                  </p>
                  {funding.checkoutUrl && isSafeCheckoutUrl(funding.checkoutUrl) ? (
                    <a
                      href={funding.checkoutUrl}
                      className={cn(buttonPrimary, "mt-5 w-full")}
                    >
                      {funding.checkoutUrl.startsWith("https://checkout.stripe.com/")
                        ? "Continue to payment"
                        : "Complete test payment"}
                    </a>
                  ) : (
                    <Link
                      to="/mock-checkout/$fundingId"
                      params={{ fundingId: funding.id }}
                      className={cn(buttonPrimary, "mt-5 w-full")}
                    >
                      Complete test payment
                    </Link>
                  )}
                  <button
                    type="button"
                    onClick={() => cancel.mutate()}
                    disabled={cancel.isPending}
                    className={cn(buttonOutline, "mt-3 w-full disabled:opacity-60")}
                  >
                    {cancel.isPending ? "Cancelling..." : "Cancel this payment"}
                  </button>
                </>
              ) : null}

              {funding.status === "succeeded" ? (
                <>
                  <p className={eyebrowItem}>Recorded</p>
                  <h2 className="mt-3 text-xl font-bold tracking-[-0.03em]">
                    Balance credited
                  </h2>
                  <p className="mt-3 text-sm leading-6 text-muted-foreground">
                    The full amount was added to the project's AI Balance. The
                    credit is listed on the project page.
                  </p>
                  <Link
                    to="/projects/$projectId"
                    params={{ projectId: funding.projectId }}
                    className={cn(buttonOutline, "mt-5 w-full")}
                  >
                    View the project
                  </Link>
                </>
              ) : null}

              {funding.status === "failed" ? (
                <>
                  <p className={eyebrowItem}>Not charged</p>
                  <h2 className="mt-3 text-xl font-bold tracking-[-0.03em]">
                    The payment failed
                  </h2>
                  <p className="mt-3 text-sm leading-6 text-muted-foreground">
                    No balance was added and no money moved. Start a new funding
                    from the project page when you are ready.
                  </p>
                  <Link
                    to="/projects/$projectId"
                    params={{ projectId: funding.projectId }}
                    className={cn(buttonOutline, "mt-5 w-full")}
                  >
                    Back to the project
                  </Link>
                </>
              ) : null}

              {funding.status === "cancelled" ? (
                <>
                  <p className={eyebrowItem}>Not charged</p>
                  <h2 className="mt-3 text-xl font-bold tracking-[-0.03em]">
                    The payment was cancelled
                  </h2>
                  <p className="mt-3 text-sm leading-6 text-muted-foreground">
                    No balance was added and no money moved. You can start again
                    from the project page.
                  </p>
                  <Link
                    to="/projects/$projectId"
                    params={{ projectId: funding.projectId }}
                    className={cn(buttonOutline, "mt-5 w-full")}
                  >
                    Back to the project
                  </Link>
                </>
              ) : null}
            </section>
          </div>
        </article>
      ) : null}
    </main>
  )
}

function statusSentence(status: string) {
  switch (status) {
    case "pending":
      return "waiting for you to finish checkout."
    case "succeeded":
      return "complete and the balance has been credited."
    case "failed":
      return "failed. No balance was added."
    case "cancelled":
      return "cancelled. No balance was added."
    default:
      return "recorded."
  }
}
