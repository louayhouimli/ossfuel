import { Link } from "@tanstack/react-router"
import { useQuery } from "@tanstack/react-query"
import { Check } from "lucide-react"
import { LogoTrace } from "@/components/brand/logo-trace"
import { Loader } from "@/components/loader"
import { authQueryOptions } from "@/lib/auth"
import { buttonPrimary, eyebrowItem, eyebrowSection, panel } from "@/lib/styles"
import { cn } from "@workspace/ui/lib/utils"

const flowSteps = [
  ["01", "Sponsor", "Choose an open-source project to support."],
  ["02", "AI Balance", "A verified payment increases that project's balance."],
  ["03", "Gateway", "A maintainer or AI client sends an authorized request."],
  ["04", "Record", "The cost is deducted and the usage is recorded."],
] as const

export function LandingPage() {
  const { data: user, isLoading: isAuthLoading } = useQuery(authQueryOptions())

  const authCta = user ? (
    <Link to="/dashboard" className={cn(buttonPrimary, "justify-center")}>
      Open dashboard
    </Link>
  ) : isAuthLoading ? (
    <Loader
      label="Checking session"
      size={16}
      className="min-h-12 justify-center px-6"
    />
  ) : (
    <Link to="/sign-in" className={cn(buttonPrimary, "justify-center")}>
      Sign in with GitHub
    </Link>
  )

  return (
    <main>
      <section className="relative isolate overflow-hidden">
        <LogoTrace className="pointer-events-none absolute top-1/2 left-[38%] -z-10 h-auto w-[105%] -translate-y-1/2" />
        <div className="mx-auto grid min-h-[calc(100svh-76px)] max-w-6xl items-center gap-12 px-5 py-12 sm:px-8 sm:py-16 lg:grid-cols-[0.95fr_1.05fr] lg:gap-20">
          <div className="max-w-xl">
            <h1 className="font-sans text-[clamp(3rem,7vw,6.5rem)] leading-[0.91] font-bold tracking-[-0.065em]">
              Fund the work. See where it goes.
            </h1>
            <p className="mt-7 max-w-lg text-base leading-7 text-muted-foreground sm:text-lg">
              OSSFuel gives each project an AI Balance. Sponsors fund it,
              maintainers use it for development, and successful requests become
              visible usage records.
            </p>
            <div className="mt-8 flex flex-col gap-3 sm:flex-row">
              <a href="#flow" className={cn(buttonPrimary, "justify-center")}>
                Follow the funding flow
              </a>
              {authCta}
            </div>
          </div>

          <div className="relative">
            <div
              className="absolute top-0 bottom-0 left-5 w-px bg-foreground/20"
              aria-hidden="true"
            />
            <div className="relative ml-0 space-y-3 sm:ml-8">
              {[
                [
                  "Sponsor",
                  "Funding verified",
                  "Support is attached to a specific project.",
                ],
                [
                  "Project AI Balance",
                  "Ready for use",
                  "Credits stay with the project they support.",
                ],
                [
                  "OSSFuel Gateway",
                  "Request authorized",
                  "A maintainer or AI client uses the project key.",
                ],
                [
                  "Usage record",
                  "Deduction recorded",
                  "Provider, model, cost, and usage stay visible.",
                ],
              ].map(([title, status, description], index) => (
                <div key={title} className={cn(panel, "relative")}>
                  <span
                    className="absolute top-6 -left-[2.05rem] flex h-4 w-4 items-center justify-center rounded-full border-2 border-background bg-foreground sm:-left-[2.55rem]"
                    aria-hidden="true"
                  >
                    <span className="h-1.5 w-1.5 rounded-full bg-background" />
                  </span>
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <p className={eyebrowItem}>0{index + 1}</p>
                      <h2 className="mt-2 font-sans text-xl font-bold tracking-[-0.04em]">
                        {title}
                      </h2>
                    </div>
                    <span className="inline-flex items-center gap-1.5 text-xs font-semibold text-muted-foreground">
                      <Check size={14} aria-hidden="true" />
                      {status}
                    </span>
                  </div>
                  <p className="mt-4 text-sm leading-6 text-muted-foreground">
                    {description}
                  </p>
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>

      <section id="why" className="border-y border-foreground/15 bg-secondary">
        <div className="mx-auto grid max-w-6xl gap-10 px-5 py-16 sm:px-8 sm:py-20 lg:grid-cols-[0.75fr_1.25fr] lg:gap-24">
          <div>
            <p className={eyebrowSection}>Why OSSFuel</p>
            <h2 className="mt-5 max-w-md font-sans text-4xl leading-tight font-bold tracking-[-0.04em] sm:text-5xl">
              AI tools should not become a private expense for maintainers.
            </h2>
          </div>
          <div className="grid gap-8 sm:grid-cols-2">
            <div className="border-t border-foreground/20 pt-5">
              <h3 className="font-semibold">Fund a project, not a black box</h3>
              <p className="mt-3 leading-7 text-muted-foreground">
                Sponsors support a specific open-source project instead of
                sending money into an account with no usage context.
              </p>
            </div>
            <div className="border-t border-foreground/20 pt-5">
              <h3 className="font-semibold">Keep usage accountable</h3>
              <p className="mt-3 leading-7 text-muted-foreground">
                OSSFuel records the project, model, provider, usage, cost, and
                balance deduction for each successful request.
              </p>
            </div>
          </div>
        </div>
      </section>

      <section
        id="flow"
        className="mx-auto max-w-6xl px-5 py-20 sm:px-8 sm:py-28"
      >
        <div className="max-w-2xl">
          <p className={eyebrowSection}>The flow</p>
          <h2 className="mt-5 font-sans text-5xl leading-[0.98] font-bold tracking-[-0.05em] sm:text-6xl">
            Follow a contribution from funding to usage.
          </h2>
        </div>
        <div className="mt-14 border-l border-foreground/25 pl-5 sm:pl-8">
          {flowSteps.map(([step, title, description]) => (
            <article
              key={step}
              className="relative border-b border-foreground/15 py-6 first:pt-0 last:border-b-0"
            >
              <span
                className="absolute top-7 -left-[1.8rem] h-2.5 w-2.5 rounded-full bg-foreground sm:-left-[2.3rem]"
                aria-hidden="true"
              />
              <p className={eyebrowItem}>{step}</p>
              <div className="mt-3 grid gap-2 sm:grid-cols-[10rem_1fr] sm:gap-8">
                <h3 className="text-lg font-semibold">{title}</h3>
                <p className="text-sm leading-6 text-muted-foreground">
                  {description}
                </p>
              </div>
            </article>
          ))}
        </div>
      </section>

      <section
        id="record"
        className="border-t border-foreground/15 bg-secondary"
      >
        <div className="mx-auto grid max-w-6xl gap-12 px-5 py-16 sm:px-8 sm:py-20 lg:grid-cols-[0.8fr_1.2fr] lg:items-start lg:gap-24">
          <div>
            <p className={eyebrowSection}>The record</p>
            <h2 className="mt-5 font-sans text-4xl leading-tight font-bold tracking-[-0.04em] sm:text-5xl">
              The balance stays tied to the work.
            </h2>
            <p className="mt-5 max-w-md leading-7 text-muted-foreground">
              Each successful request creates a record that explains what
              happened to the project balance.
            </p>
          </div>
          <div className="border border-foreground/20 bg-background p-5 sm:p-8">
            <div className="flex items-center justify-between border-b border-foreground/15 pb-5">
              <div>
                <p className={eyebrowItem}>Usage record</p>
                <p className="mt-2 font-sans text-2xl font-bold tracking-[-0.04em]">
                  Project request
                </p>
              </div>
              <span className="inline-flex items-center gap-1.5 text-xs font-semibold text-muted-foreground">
                <Check size={14} aria-hidden="true" />
                Successful
              </span>
            </div>
            <dl className="divide-y divide-foreground/15">
              {[
                ["Project", "Open-source project"],
                ["Request", "AI Gateway request"],
                ["Provider", "OpenRouter"],
                ["Usage", "Input and output tokens"],
                ["Balance", "Deduction recorded"],
              ].map(([label, value]) => (
                <div
                  key={label}
                  className="grid gap-1 py-4 text-sm sm:grid-cols-[9rem_1fr] sm:gap-6"
                >
                  <dt className="text-muted-foreground">{label}</dt>
                  <dd className="font-semibold">{value}</dd>
                </div>
              ))}
            </dl>
          </div>
        </div>
      </section>

      <section className="border-t border-foreground/15">
        <div className="mx-auto flex max-w-6xl flex-col gap-8 px-5 py-16 sm:px-8 sm:py-20 lg:flex-row lg:items-end lg:justify-between">
          <div className="max-w-2xl">
            <p className={eyebrowSection}>Project status</p>
            <h2 className="mt-5 font-sans text-4xl leading-tight font-bold tracking-[-0.04em] sm:text-5xl">
              The model is defined. The platform is in early development.
            </h2>
            <p className="mt-5 max-w-xl leading-7 text-muted-foreground">
              GitHub sign-in and project registration are in place, so
              maintainers can link a public repository to a project. Funding, AI
              Balance management, gateway access, and usage records come next.
            </p>
          </div>
          {authCta}
        </div>
      </section>
    </main>
  )
}
