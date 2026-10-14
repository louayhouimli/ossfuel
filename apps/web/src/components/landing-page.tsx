import { useState } from "react"
import { Link } from "@tanstack/react-router"
import { Check, Menu, Moon, Sun, X } from "lucide-react"
import { Logo } from "@/components/brand/logo"
import { useTheme } from "@/components/theme-provider"

const flowSteps = [
  ["01", "Sponsor", "Choose an open-source project to support."],
  ["02", "AI Balance", "A verified payment increases that project's balance."],
  ["03", "Gateway", "A maintainer or AI client sends an authorized request."],
  ["04", "Record", "The cost is deducted and the usage is recorded."],
] as const

export function LandingPage() {
  const [menuOpen, setMenuOpen] = useState(false)
  const { theme, setTheme } = useTheme()
  const toggleTheme = () => setTheme(theme === "dark" ? "light" : "dark")

  return (
    <div className="min-h-svh overflow-x-hidden bg-background text-foreground">
      <header className="border-b border-foreground/15">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-5 py-5 sm:px-8">
          <Link
            to="/"
            aria-label="OSSFuel home"
            className="rounded-sm focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
          >
            <Logo size={30} />
          </Link>
          <nav
            className="hidden items-center gap-8 text-sm font-medium md:flex"
            aria-label="Primary navigation"
          >
            <a
              href="#why"
              className="focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
            >
              Why OSSFuel
            </a>
            <a
              href="#flow"
              className="focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
            >
              The flow
            </a>
            <a
              href="#record"
              className="focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
            >
              The record
            </a>
          </nav>
          <div className="hidden items-center gap-2 md:flex">
            <button
              type="button"
              onClick={toggleTheme}
              className="inline-flex min-h-11 items-center gap-2 rounded-sm border border-foreground/20 px-3 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
              aria-label={`Switch to ${theme === "dark" ? "light" : "dark"} theme`}
            >
              {theme === "dark" ? (
                <Sun size={16} aria-hidden="true" />
              ) : (
                <Moon size={16} aria-hidden="true" />
              )}
              {theme === "dark" ? "White mode" : "Dark mode"}
            </button>

            <Link
              to="/login"
              className="min-h-11 rounded-sm bg-primary px-4 py-3 text-sm font-semibold text-primary-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
            >
              Sign in with GitHub
            </Link>
          </div>
          <button
            type="button"
            className="inline-flex min-h-11 items-center gap-2 rounded-sm border border-foreground/25 px-3 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring md:hidden"
            aria-expanded={menuOpen}
            aria-controls="mobile-menu"
            onClick={() => setMenuOpen((open) => !open)}
          >
            {menuOpen ? (
              <X size={16} aria-hidden="true" />
            ) : (
              <Menu size={16} aria-hidden="true" />
            )}
            {menuOpen ? "Close" : "Menu"}
          </button>
        </div>
        {menuOpen ? (
          <nav
            id="mobile-menu"
            className="border-t border-foreground/15 px-5 py-4 md:hidden"
            aria-label="Mobile navigation"
          >
            <div className="mx-auto flex max-w-6xl flex-col gap-1">
              {[
                ["Why OSSFuel", "#why"],
                ["The flow", "#flow"],
                ["The record", "#record"],
              ].map(([label, href]) => (
                <a
                  key={href}
                  href={href}
                  onClick={() => setMenuOpen(false)}
                  className="min-h-11 rounded-sm px-2 py-3 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                >
                  {label}
                </a>
              ))}
              <button
                type="button"
                onClick={toggleTheme}
                className="mt-2 inline-flex min-h-11 items-center gap-2 rounded-sm border border-foreground/20 px-4 py-3 text-left text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
              >
                {theme === "dark" ? (
                  <Sun size={16} aria-hidden="true" />
                ) : (
                  <Moon size={16} aria-hidden="true" />
                )}
                {theme === "dark"
                  ? "Switch to white mode"
                  : "Switch to dark mode"}
              </button>
              <Link
                to="/login"
                className="mt-2 min-h-11 rounded-sm bg-primary px-4 py-3 text-center text-sm font-semibold text-primary-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
              >
                Sign in with GitHub
              </Link>
            </div>
          </nav>
        ) : null}
      </header>

      <main>
        <section className="mx-auto grid min-h-[calc(100svh-76px)] max-w-6xl items-center gap-12 px-5 py-12 sm:px-8 sm:py-16 lg:grid-cols-[0.95fr_1.05fr] lg:gap-20">
          <div className="max-w-xl">
            <p className="mb-5 text-sm font-semibold tracking-[0.18em] text-muted-foreground uppercase">
              Funding for open-source AI work
            </p>
            <h1 className="font-sans text-[clamp(3rem,7vw,6.5rem)] leading-[0.91] font-bold tracking-[-0.065em]">
              Fund the work. See where it goes.
            </h1>
            <p className="mt-7 max-w-lg text-base leading-7 text-muted-foreground sm:text-lg">
              OSSFuel gives each project an AI Balance. Sponsors fund it,
              maintainers use it for development, and successful requests become
              visible usage records.
            </p>
            <div className="mt-8 flex flex-col gap-3 sm:flex-row">
              <a
                href="#flow"
                className="min-h-12 rounded-sm bg-primary px-6 py-3 text-center font-semibold text-primary-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
              >
                Follow the funding flow
              </a>
              <Link
                to="/login"
                className="min-h-12 rounded-sm border border-foreground/30 px-6 py-3 text-center font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
              >
                Sign in with GitHub
              </Link>
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
                <div
                  key={title}
                  className="relative border border-foreground/20 bg-secondary p-5 sm:p-6"
                >
                  <span
                    className="absolute top-6 -left-[2.05rem] flex h-4 w-4 items-center justify-center rounded-full border-2 border-background bg-foreground sm:-left-[2.55rem]"
                    aria-hidden="true"
                  >
                    <span className="h-1.5 w-1.5 rounded-full bg-background" />
                  </span>
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <p className="text-xs font-semibold tracking-[0.16em] text-muted-foreground uppercase">
                        0{index + 1}
                      </p>
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
        </section>

        <section
          id="why"
          className="border-y border-foreground/15 bg-secondary"
        >
          <div className="mx-auto grid max-w-6xl gap-10 px-5 py-16 sm:px-8 sm:py-20 lg:grid-cols-[0.75fr_1.25fr] lg:gap-24">
            <div>
              <p className="text-sm font-semibold tracking-[0.18em] text-muted-foreground uppercase">
                Why OSSFuel
              </p>
              <h2 className="mt-5 max-w-md font-sans text-4xl leading-tight font-bold tracking-[-0.04em] sm:text-5xl">
                AI tools should not become a private expense for maintainers.
              </h2>
            </div>
            <div className="grid gap-8 sm:grid-cols-2">
              <div className="border-t border-foreground/20 pt-5">
                <h3 className="font-semibold">
                  Fund a project, not a black box
                </h3>
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
            <p className="text-sm font-semibold tracking-[0.18em] text-muted-foreground uppercase">
              The flow
            </p>
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
                <p className="text-xs font-semibold tracking-[0.16em] text-muted-foreground uppercase">
                  {step}
                </p>
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
              <p className="text-sm font-semibold tracking-[0.18em] text-muted-foreground uppercase">
                The record
              </p>
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
                  <p className="text-xs font-semibold tracking-[0.16em] text-muted-foreground uppercase">
                    Usage record
                  </p>
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
              <p className="text-sm font-semibold tracking-[0.18em] text-muted-foreground uppercase">
                Project status
              </p>
              <h2 className="mt-5 font-sans text-4xl leading-tight font-bold tracking-[-0.04em] sm:text-5xl">
                The model is defined. The platform is in early development.
              </h2>
              <p className="mt-5 max-w-xl leading-7 text-muted-foreground">
                The next product work covers GitHub authentication, project
                registration, external funding, AI Balance management, gateway
                access, and usage records.
              </p>
            </div>
            <Link
              to="/login"
              className="min-h-12 rounded-sm bg-primary px-6 py-3 text-center font-semibold text-primary-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
            >
              Sign in with GitHub
            </Link>
          </div>
        </section>
      </main>

      <footer className="border-t border-foreground/15">
        <div className="mx-auto flex max-w-6xl flex-col gap-4 px-5 py-8 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between sm:px-8">
          <Logo size={24} />
          <p>Open-source funding with the AI spend in view.</p>
        </div>
      </footer>
    </div>
  )
}
