import { createFileRoute, Link } from "@tanstack/react-router"
import { useQuery } from "@tanstack/react-query"
import { PageHeader } from "@/components/page-header"
import { authQueryOptions } from "@/lib/auth"
import {
  buttonOutlineCompact,
  buttonPrimaryCompact,
  eyebrowItem,
  panel,
} from "@/lib/styles"
import { cn } from "@workspace/ui/lib/utils"

export const Route = createFileRoute("/_site/_authenticated/dashboard")({
  head: () => ({
    meta: [
      { title: "Dashboard | OSSFuel" },
      {
        name: "description",
        content:
          "Register the open-source projects you maintain so sponsors can fund their AI Balance.",
      },
    ],
  }),
  component: Dashboard,
})

const steps = [
  {
    step: "01",
    title: "Register a project",
    description:
      "Import one of your public GitHub repositories. OSSFuel retrieves its details and registers it as a project.",
    to: "/import",
    label: "Import a repository",
    primary: true,
  },
  {
    step: "02",
    title: "Browse projects",
    description:
      "See every project registered on OSSFuel and the repositories behind them.",
    to: "/projects",
    label: "View projects",
    primary: false,
  },
] as const

function Dashboard() {
  const { data: user } = useQuery(authQueryOptions())

  return (
    <main className="mx-auto max-w-6xl px-5 py-8 sm:px-8 sm:py-12">
      <PageHeader
        eyebrow="Dashboard"
        title={`Welcome, ${user?.name ?? user?.login ?? "maintainer"}`}
        description="Register the open-source projects you maintain so sponsors can fund their AI Balance."
      />

      <div className="mt-10 grid gap-3 sm:grid-cols-2">
        {steps.map(({ step, title, description, to, label, primary }) => (
          <div key={step} className={cn(panel, "flex flex-col")}>
            <p className={eyebrowItem}>{step}</p>
            <h2 className="mt-3 text-xl font-bold tracking-[-0.03em]">
              {title}
            </h2>
            <p className="mt-3 flex-1 text-sm leading-6 text-muted-foreground">
              {description}
            </p>
            <Link
              to={to}
              className={cn(
                primary ? buttonPrimaryCompact : buttonOutlineCompact,
                "mt-6 w-fit"
              )}
            >
              {label}
            </Link>
          </div>
        ))}
      </div>
    </main>
  )
}
