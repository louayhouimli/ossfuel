import { useState } from "react"
import { Link } from "@tanstack/react-router"
import { useQuery } from "@tanstack/react-query"
import { ArrowRight, Check, Copy } from "lucide-react"
import { LogoTrace } from "@/components/brand/logo-trace"
import { Loader } from "@/components/loader"
import { authQueryOptions } from "@/lib/auth"
import { buttonOutline, buttonPrimary, eyebrowItem, eyebrowSection, panel } from "@/lib/styles"
import { cn } from "@workspace/ui/lib/utils"

const flowSteps = [
  ["01", "Sponsor", "Choose an open-source project and contribute to its dedicated balance."],
  ["02", "AI Balance", "Verified funding atomically credits the project balance in USD cents."],
  ["03", "Gateway", "Maintainers generate scoped API keys for tools like OpenCode, Aider, and Cursor."],
  ["04", "Record", "Each request debits the balance and logs verified token usage and cost."],
] as const

const integrationTabs = [
  {
    id: "opencode",
    label: "OpenCode",
    filename: "opencode.json",
    code: `{
  "$schema": "https://opencode.ai/config.json",
  "provider": {
    "ossfuel": {
      "npm": "@ai-sdk/openai",
      "options": {
        "baseURL": "https://api.ossfuel.louayhouimli.dev/v1",
        "apiKey": "{env:OSSFUEL_API_KEY}"
      }
    }
  }
}`,
  },
  {
    id: "aider",
    label: "Aider CLI",
    filename: "Terminal",
    code: `# Configure environment variables
export OPENAI_API_BASE=https://api.ossfuel.louayhouimli.dev/v1
export OPENAI_API_KEY=ossf_your_project_key_here

# Launch Aider using the funded model
aider --model openrouter/free`,
  },
  {
    id: "cursor",
    label: "Cursor / Continue",
    filename: "IDE Settings",
    code: `// Custom OpenAI API Provider Settings
Base URL: https://api.ossfuel.louayhouimli.dev/v1
API Key:  ossf_your_project_key_here
Model:    openrouter/free (or any supported model)`,
  },
  {
    id: "curl",
    label: "cURL",
    filename: "Terminal",
    code: `curl https://api.ossfuel.louayhouimli.dev/v1/chat/completions \\
  -H "Authorization: Bearer ossf_your_project_key_here" \\
  -H "Content-Type: application/json" \\
  -d '{
    "model": "openrouter/free",
    "messages": [
      { "role": "user", "content": "Explain this pull request" }
    ]
  }'`,
  },
]

export function LandingPage() {
  const { data: user, isLoading: isAuthLoading } = useQuery(authQueryOptions())
  const [activeTab, setActiveTab] = useState(integrationTabs[0].id)
  const [copied, setCopied] = useState(false)

  const activeSnippet =
    integrationTabs.find((t) => t.id === activeTab) ?? integrationTabs[0]

  const handleCopy = async () => {
    await navigator.clipboard.writeText(activeSnippet.code)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  const authCta = user ? (
    <Link to="/dashboard" className={cn(buttonOutline, "justify-center")}>
      Open dashboard
    </Link>
  ) : isAuthLoading ? (
    <Loader
      label="Checking session"
      size={16}
      className="min-h-12 justify-center px-6"
    />
  ) : (
    <Link to="/sign-in" className={cn(buttonOutline, "justify-center")}>
      Sign in with GitHub
    </Link>
  )

  return (
    <main>
      {/* Hero Section */}
      <section className="relative isolate overflow-hidden">
        <LogoTrace className="pointer-events-none absolute top-1/2 left-[38%] -z-10 h-auto w-[105%] -translate-y-1/2" />
        <div className="mx-auto grid min-h-[calc(100svh-76px)] max-w-6xl items-center gap-12 px-5 py-12 sm:px-8 sm:py-16 lg:grid-cols-[0.95fr_1.05fr] lg:gap-20">
          <div className="max-w-xl">
            <h1 className="font-sans text-[clamp(3rem,7vw,6.5rem)] leading-[0.91] font-bold tracking-[-0.065em]">
              Fund the work. See where it goes.
            </h1>
            <p className="mt-7 max-w-lg text-base leading-7 text-muted-foreground sm:text-lg">
              OSSFuel gives each open-source project an AI Balance. Sponsors fund
              it, maintainers spend it on coding tools via an OpenAI-compatible
              gateway, and every request is audited.
            </p>
            <div className="mt-8 flex flex-col gap-3 sm:flex-row">
              <Link to="/projects" className={cn(buttonPrimary, "justify-center")}>
                Explore projects
                <ArrowRight size={16} aria-hidden="true" />
              </Link>
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
                  "Sponsor Funding",
                  "Verified",
                  "Support attaches directly to a specific open-source project.",
                ],
                [
                  "Project AI Balance",
                  "Credited",
                  "Credits remain with the project in transparent USD minor units.",
                ],
                [
                  "OSSFuel Gateway",
                  "Authorized",
                  "Maintainers use scoped API keys with OpenCode, Aider, and Cursor.",
                ],
                [
                  "Usage Audit",
                  "Recorded",
                  "Model, token count, cost, and deductions stay fully visible.",
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

      {/* Why OSSFuel Section */}
      <section id="why" className="border-y border-foreground/15 bg-secondary">
        <div className="mx-auto grid max-w-6xl gap-10 px-5 py-16 sm:px-8 sm:py-20 lg:grid-cols-[0.75fr_1.25fr] lg:gap-24">
          <div>
            <p className={eyebrowSection}>Why OSSFuel</p>
            <h2 className="mt-5 max-w-md font-sans text-4xl leading-tight font-bold tracking-[-0.04em] sm:text-5xl">
              AI tools should not become a private expense for maintainers.
            </h2>
          </div>
          <div className="grid gap-8 sm:grid-cols-3">
            <div className="border-t border-foreground/20 pt-5">
              <h3 className="font-semibold">Fund a project, not a black box</h3>
              <p className="mt-3 text-sm leading-6 text-muted-foreground">
                Sponsors fund specific repositories instead of depositing money
                into vague tip jars without visibility into how AI is spent.
              </p>
            </div>
            <div className="border-t border-foreground/20 pt-5">
              <h3 className="font-semibold">Drop-in AI Gateway</h3>
              <p className="mt-3 text-sm leading-6 text-muted-foreground">
                Works out of the box with OpenCode, Aider, Cursor, and any
                client supporting the OpenAI wire format.
              </p>
            </div>
            <div className="border-t border-foreground/20 pt-5">
              <h3 className="font-semibold">Accountable Token Auditing</h3>
              <p className="mt-3 text-sm leading-6 text-muted-foreground">
                Every request logs input/output tokens, deduction costs, and
                model records with microsecond accuracy.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* Developer Gateway Integration Showcase */}
      <section id="gateway" className="border-b border-foreground/15">
        <div className="mx-auto max-w-6xl px-5 py-20 sm:px-8 sm:py-28">
          <div className="max-w-2xl">
            <p className={eyebrowSection}>Developer Integration</p>
            <h2 className="mt-5 font-sans text-5xl leading-[0.98] font-bold tracking-[-0.05em] sm:text-6xl">
              Plug into your coding assistant.
            </h2>
            <p className="mt-6 text-base leading-7 text-muted-foreground sm:text-lg">
              OSSFuel exposes a standard OpenAI-compatible API gateway. Generate
              a project key from your dashboard, set the base URL, and start
              coding.
            </p>
          </div>

          <div className="mt-12 rounded-sm border border-foreground/15 bg-background">
            <div className="flex flex-wrap items-center justify-between border-b border-foreground/15 bg-secondary/40 px-4 py-2.5">
              <div className="flex flex-wrap items-center gap-1">
                {integrationTabs.map((tab) => (
                  <button
                    key={tab.id}
                    type="button"
                    onClick={() => setActiveTab(tab.id)}
                    className={cn(
                      "rounded-sm px-3 py-1.5 text-xs font-semibold transition-colors",
                      activeTab === tab.id
                        ? "bg-foreground text-background"
                        : "text-muted-foreground hover:bg-foreground/5 hover:text-foreground"
                    )}
                  >
                    {tab.label}
                  </button>
                ))}
              </div>
              <div className="flex items-center gap-3">
                <span className="hidden font-mono text-xs text-muted-foreground sm:inline">
                  {activeSnippet.filename}
                </span>
                <button
                  type="button"
                  onClick={handleCopy}
                  className="inline-flex items-center gap-1.5 rounded-sm border border-foreground/20 px-2.5 py-1 text-xs font-semibold hover:bg-foreground/5"
                >
                  {copied ? (
                    <>
                      <Check size={12} className="text-emerald-500" />
                      <span>Copied</span>
                    </>
                  ) : (
                    <>
                      <Copy size={12} />
                      <span>Copy</span>
                    </>
                  )}
                </button>
              </div>
            </div>

            <pre className="overflow-x-auto p-5 font-mono text-xs leading-relaxed sm:text-sm">
              <code>{activeSnippet.code}</code>
            </pre>
          </div>
        </div>
      </section>

      {/* The Flow Section */}
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

      {/* The Record Section */}
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
              Each successful request creates an immutable record that explains
              what happened to the project balance.
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
              <span className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-500">
                <Check size={14} aria-hidden="true" />
                200 OK
              </span>
            </div>
            <dl className="divide-y divide-foreground/15">
              {[
                ["Project", "torvalds/linux"],
                ["Request", "/api/v1/chat/completions"],
                ["Model", "openrouter/free"],
                ["Usage", "1,420 prompt · 380 completion tokens"],
                ["Deduction", "$0.0036 USD deducted"],
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

      {/* Project Status / Final CTA */}
      <section className="border-t border-foreground/15">
        <div className="mx-auto flex max-w-6xl flex-col gap-8 px-5 py-16 sm:px-8 sm:py-20 lg:flex-row lg:items-end lg:justify-between">
          <div className="max-w-2xl">
            <p className={eyebrowSection}>Get started</p>
            <h2 className="mt-5 font-sans text-4xl leading-tight font-bold tracking-[-0.04em] sm:text-5xl">
              Fund open-source AI today.
            </h2>
            <p className="mt-5 max-w-xl leading-7 text-muted-foreground">
              GitHub authentication, project management, sponsor checkout, and
              the OpenAI-compatible AI gateway are fully live. Link your
              repository and fuel your development.
            </p>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row">
            <Link to="/projects" className={cn(buttonPrimary, "justify-center")}>
              Browse projects
            </Link>
            {authCta}
          </div>
        </div>
      </section>
    </main>
  )
}
