import { createFileRoute, Link, useNavigate } from "@tanstack/react-router"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { ArrowLeft } from "lucide-react"
import { FundingStatusBadge } from "@/components/funding-status"
import { Loader } from "@/components/loader"
import { PageHeader } from "@/components/page-header"
import { ApiError } from "@/lib/api"
import {
  completeMockPayment,
  formatMoney,
  fundingQueryOptions,
} from "@/lib/funding"
import { buttonOutline, buttonPrimary, eyebrowItem, panel } from "@/lib/styles"
import { cn } from "@workspace/ui/lib/utils"

export const Route = createFileRoute(
  "/_site/_authenticated/mock-checkout/$fundingId"
)({
  head: () => ({
    meta: [
      { title: "Test checkout | OSSFuel" },
      {
        name: "description",
        content:
          "A local test checkout that completes a funding payment without charging a card.",
      },
    ],
  }),
  component: MockCheckoutPage,
})

function MockCheckoutPage() {
  const { fundingId } = Route.useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const {
    data: funding,
    isLoading,
    isError,
    error,
  } = useQuery(fundingQueryOptions(fundingId))

  const mutation = useMutation({
    mutationFn: (outcome: "succeeded" | "failed" | "cancelled") =>
      completeMockPayment(fundingId, outcome),
    onSuccess: async (result) => {
      queryClient.setQueryData(fundingQueryOptions(fundingId).queryKey, result)
      await queryClient.invalidateQueries({
        queryKey: ["balance", result.projectId],
      })
      await queryClient.invalidateQueries({ queryKey: ["fundings"] })
      await navigate({
        to: "/fundings/$fundingId",
        params: { fundingId },
      })
    },
  })

  const notFound = isError && error instanceof ApiError && error.status === 404

  return (
    <main className="mx-auto max-w-3xl px-5 py-8 sm:px-8 sm:py-12">
      <Link
        to="/fundings/$fundingId"
        params={{ fundingId }}
        className="inline-flex min-h-11 items-center gap-2 rounded-sm text-sm font-semibold text-muted-foreground hover:text-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        <ArrowLeft size={16} aria-hidden="true" />
        Back to the funding
      </Link>

      {isLoading ? (
        <Loader label="Loading the test checkout" className="mt-8" />
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
          The test checkout could not be loaded. Try again shortly.
        </p>
      ) : null}

      {funding ? (
        <>
          <PageHeader
            className="mt-5"
            eyebrow="Test checkout"
            title={funding.projectFullName}
            description={`You are adding ${formatMoney(funding.amountMinor, funding.currency)} to this project's AI Balance. This page stands in for a payment provider, so no card is charged.`}
          />

          <section className={cn(panel, "mt-8")}>
            <div className="flex flex-wrap items-center justify-between gap-4">
              <div>
                <p className={eyebrowItem}>Amount due</p>
                <p className="mt-2 font-sans text-3xl font-bold tracking-[-0.04em]">
                  {formatMoney(funding.amountMinor, funding.currency)}
                </p>
              </div>
              <FundingStatusBadge status={funding.status} />
            </div>

            {funding.status === "pending" ? (
              <div className="mt-6 flex flex-col gap-3 border-t border-foreground/15 pt-6">
                <button
                  type="button"
                  onClick={() => mutation.mutate("succeeded")}
                  disabled={mutation.isPending}
                  className={cn(buttonPrimary, "w-full disabled:opacity-60")}
                >
                  {mutation.isPending && mutation.variables === "succeeded"
                    ? "Completing payment"
                    : `Pay ${formatMoney(funding.amountMinor, funding.currency)}`}
                </button>

                <button
                  type="button"
                  onClick={() => mutation.mutate("failed")}
                  disabled={mutation.isPending}
                  className={cn(buttonOutline, "w-full disabled:opacity-60")}
                >
                  {mutation.isPending && mutation.variables === "failed"
                    ? "Simulating failure"
                    : "Simulate a failed payment"}
                </button>

                <button
                  type="button"
                  onClick={() => mutation.mutate("cancelled")}
                  disabled={mutation.isPending}
                  className={cn(buttonOutline, "w-full disabled:opacity-60")}
                >
                  {mutation.isPending && mutation.variables === "cancelled"
                    ? "Cancelling payment"
                    : "Cancel payment"}
                </button>
              </div>
            ) : (
              <div className="mt-6 border-t border-foreground/15 pt-6">
                <p className="text-sm leading-6 text-muted-foreground">
                  This payment is already finished. Its result is on the funding
                  page.
                </p>
                <Link
                  to="/fundings/$fundingId"
                  params={{ fundingId }}
                  className={cn(buttonOutline, "mt-5 w-full")}
                >
                  View the result
                </Link>
              </div>
            )}

            {mutation.isError ? (
              <p className="mt-4 text-sm font-semibold" role="alert">
                {mutation.error instanceof ApiError
                  ? mutation.error.message
                  : "The test payment could not be completed. Try again."}
              </p>
            ) : null}
          </section>
        </>
      ) : null}
    </main>
  )
}
