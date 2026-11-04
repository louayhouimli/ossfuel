import { useState, type FormEvent } from "react"
import { useMutation, useQueryClient } from "@tanstack/react-query"
import { Link, useNavigate } from "@tanstack/react-router"
import { ApiError } from "@/lib/api"
import {
  createFunding,
  formatMoney,
  MAX_FUNDING_MINOR,
  MIN_FUNDING_MINOR,
  parseAmountToMinor,
} from "@/lib/funding"
import { buttonPrimary, eyebrowItem, panel } from "@/lib/styles"
import { cn } from "@workspace/ui/lib/utils"

const limits = `The minimum is ${formatMoney(MIN_FUNDING_MINOR)} and the maximum is ${formatMoney(MAX_FUNDING_MINOR)}.`

type FundProjectFormProps = {
  projectId: string
}

/**
 * Starts a sponsor payment for one project.
 *
 * The amount stays a string until submit so the conversion to minor units can
 * reject anything that is not a plain dollar amount, instead of letting a float
 * round the payment.
 */
export function FundProjectForm({ projectId }: FundProjectFormProps) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [amount, setAmount] = useState("25.00")
  const [validationError, setValidationError] = useState<string | null>(null)

  const mutation = useMutation({
    mutationFn: (amountMinor: number) => createFunding(projectId, amountMinor),
    onSuccess: async (funding) => {
      await queryClient.invalidateQueries({ queryKey: ["fundings"] })

      if (funding.checkoutUrl) {
        window.location.assign(funding.checkoutUrl)
        return
      }

      await navigate({
        to: "/fundings/$fundingId",
        params: { fundingId: funding.id },
      })
    },
  })

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setValidationError(null)
    mutation.reset()

    const amountMinor = parseAmountToMinor(amount)

    if (amountMinor === null) {
      setValidationError("Enter an amount like 25.00.")
      return
    }

    if (amountMinor < MIN_FUNDING_MINOR || amountMinor > MAX_FUNDING_MINOR) {
      setValidationError(limits)
      return
    }

    mutation.mutate(amountMinor)
  }

  const conflict =
    mutation.error instanceof ApiError && mutation.error.status === 409
  const message =
    validationError ??
    (conflict || !mutation.error ? null : errorMessage(mutation.error))

  return (
    <section className={panel} aria-labelledby="fund-heading">
      <p className={eyebrowItem}>Fund this project</p>
      <h2
        id="fund-heading"
        className="mt-3 text-xl font-bold tracking-[-0.03em]"
      >
        Add to the AI Balance
      </h2>
      <p className="mt-3 text-sm leading-6 text-muted-foreground">
        Your payment goes to this project at full value. OSSFuel keeps no fee.
      </p>

      <form onSubmit={submit} className="mt-6">
        <label htmlFor="funding-amount" className={eyebrowItem}>
          Amount in USD
        </label>
        <div className="mt-2 flex items-center border border-foreground/20 bg-background focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-ring">
          <span className="pl-4 text-base font-semibold text-muted-foreground">
            $
          </span>
          <input
            id="funding-amount"
            name="amount"
            type="text"
            inputMode="decimal"
            autoComplete="off"
            value={amount}
            onChange={(event) => setAmount(event.target.value)}
            aria-invalid={message ? true : undefined}
            aria-describedby="funding-amount-help"
            className="min-h-12 w-full bg-transparent px-2 py-3 text-lg font-semibold outline-none"
          />
        </div>
        <p
          id="funding-amount-help"
          className="mt-2 text-xs leading-5 text-muted-foreground"
        >
          {limits}
        </p>

        <button
          type="submit"
          disabled={mutation.isPending}
          className={cn(buttonPrimary, "mt-5 w-full disabled:opacity-60")}
        >
          {mutation.isPending ? "Opening checkout" : "Continue to payment"}
        </button>
      </form>

      {message ? (
        <p className="mt-4 text-sm font-semibold text-foreground" role="alert">
          {message}
        </p>
      ) : null}

      {conflict ? (
        <p
          className="mt-4 text-sm leading-6 text-muted-foreground"
          role="alert"
        >
          You already have a pending funding for this project.{" "}
          <Link
            to="/fundings"
            className="font-semibold underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
          >
            View your funding
          </Link>
          .
        </p>
      ) : null}

      <p className="mt-6 border-t border-foreground/15 pt-4 text-xs leading-5 text-muted-foreground">
        Payments run in test mode on this build. No card is charged.
      </p>
    </section>
  )
}

function errorMessage(error: unknown) {
  return error instanceof ApiError
    ? error.message
    : "The payment could not be started. Try again."
}
