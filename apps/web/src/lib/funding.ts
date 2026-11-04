import { keepPreviousData, queryOptions } from "@tanstack/react-query"
import { apiFetch } from "./api"

export type Balance = {
  projectId: string
  amountMinor: number
  currency: string
}

export type BalanceTransactionSponsor = {
  id: string
  login: string
  name: string | null
  avatarUrl: string | null
}

export type BalanceTransaction = {
  id: string
  amountMinor: number
  type: string
  status: string
  description: string | null
  sponsor: BalanceTransactionSponsor | null
  createdAt: string
}

export type BalanceTransactionList = {
  items: BalanceTransaction[]
  page: number
  pageSize: number
  totalCount: number
}

export type FundingStatus = "pending" | "succeeded" | "failed" | "cancelled"

export type Funding = {
  id: string
  projectId: string
  projectFullName: string
  amountMinor: number
  currency: string
  status: FundingStatus
  checkoutUrl: string | null
  createdAt: string
  completedAt: string | null
}

export type FundingList = {
  items: Funding[]
  page: number
  pageSize: number
  totalCount: number
}

/** Server defaults, kept in step with `FundingOptions`. */
export const MIN_FUNDING_MINOR = 100
export const MAX_FUNDING_MINOR = 1_000_000

export const balanceQueryOptions = (projectId: string) =>
  queryOptions({
    queryKey: ["balance", projectId],
    queryFn: () => apiFetch<Balance>(`/projects/${projectId}/balance`),
  })

export const balanceTransactionsQueryOptions = (
  projectId: string,
  page: number,
  pageSize: number
) =>
  queryOptions({
    queryKey: ["balance", projectId, "transactions", { page, pageSize }],
    queryFn: () =>
      apiFetch<BalanceTransactionList>(
        `/projects/${projectId}/balance/transactions?page=${page}&pageSize=${pageSize}`
      ),
    placeholderData: keepPreviousData,
  })

export const fundingsQueryOptions = (page: number, pageSize: number) =>
  queryOptions({
    queryKey: ["fundings", { page, pageSize }],
    queryFn: () =>
      apiFetch<FundingList>(`/fundings?page=${page}&pageSize=${pageSize}`),
    placeholderData: keepPreviousData,
  })

export const fundingQueryOptions = (id: string) =>
  queryOptions({
    queryKey: ["fundings", id],
    queryFn: () => apiFetch<Funding>(`/fundings/${id}`),
    retry: false,
  })

export function createFunding(
  projectId: string,
  amountMinor: number
): Promise<Funding> {
  return apiFetch<Funding>("/fundings", {
    method: "POST",
    body: JSON.stringify({ projectId, amountMinor }),
  })
}

export function completeMockPayment(
  fundingId: string,
  outcome: "succeeded" | "failed" | "cancelled"
): Promise<Funding> {
  return apiFetch<Funding>(`/fundings/${fundingId}/mock-complete`, {
    method: "POST",
    body: JSON.stringify({ outcome }),
  })
}

export function formatMoney(amountMinor: number, currency = "usd"): string {
  return new Intl.NumberFormat(undefined, {
    style: "currency",
    currency: currency.toUpperCase(),
  }).format(amountMinor / 100)
}

/**
 * Converts a typed dollar amount to minor units without going through a float,
 * so 0.1 + 0.2 style rounding cannot creep into a payment amount. Returns null
 * when the text is not a plain amount with at most two decimals.
 */
export function parseAmountToMinor(value: string): number | null {
  const match = value.trim().match(/^(\d+)(?:\.(\d{1,2}))?$/)

  if (!match) {
    return null
  }

  return Number(match[1]) * 100 + Number((match[2] ?? "").padEnd(2, "0"))
}
