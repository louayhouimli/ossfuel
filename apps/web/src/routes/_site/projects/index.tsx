import { createFileRoute, Link, useNavigate } from "@tanstack/react-router"
import { useQuery } from "@tanstack/react-query"
import { ArrowLeft, ArrowRight, Star } from "lucide-react"
import { Loader } from "@/components/loader"
import { PageHeader } from "@/components/page-header"
import { projectsQueryOptions } from "@/lib/projects"
import {
  buttonOutlineCompact,
  buttonPrimary,
  eyebrowItem,
  panel,
} from "@/lib/styles"
import { cn } from "@workspace/ui/lib/utils"

const PAGE_SIZE = 12

export const Route = createFileRoute("/_site/projects/")({
  validateSearch: (search: Record<string, unknown>): { page?: number } => ({
    page: Math.max(1, Number(search.page) || 1),
  }),
  head: () => ({
    meta: [
      { title: "Projects | OSSFuel" },
      {
        name: "description",
        content:
          "Open-source projects registered by their maintainers on OSSFuel, each linked to its public GitHub repository.",
      },
    ],
  }),
  component: ProjectsPage,
})

function ProjectsPage() {
  const { page = 1 } = Route.useSearch()
  const navigate = useNavigate()
  const { data, isLoading, isFetching, isError } = useQuery(
    projectsQueryOptions(page, PAGE_SIZE)
  )

  const totalPages = data
    ? Math.max(1, Math.ceil(data.totalCount / data.pageSize))
    : 1

  const goToPage = (next: number) => {
    void navigate({ to: "/projects", search: { page: next } })
    window.scrollTo({ top: 0 })
  }

  return (
    <main className="mx-auto max-w-6xl px-5 py-8 sm:px-8 sm:py-12">
      <PageHeader
        eyebrow="Projects"
        title="Open-source projects building with OSSFuel."
        description="Every project here is registered by a maintainer and linked to its public GitHub repository. Sponsors will fund these projects' AI balances."
      />

      {isLoading ? (
        <Loader label="Loading projects." className="mt-10" />
      ) : null}

      {isError ? (
        <p
          role="alert"
          className="mt-10 border border-destructive/40 bg-destructive/10 px-5 py-4 text-sm text-foreground"
        >
          Projects could not be loaded. Try again shortly.
        </p>
      ) : null}

      {data && data.items.length === 0 ? (
        <div className={cn(panel, "mt-10")}>
          <h2 className="font-sans text-2xl font-bold tracking-[-0.04em]">
            No projects are registered yet.
          </h2>
          <p className="mt-3 max-w-lg leading-7 text-muted-foreground">
            A maintainer signs in with GitHub and imports their repository to
            create the first project.
          </p>
          <Link to="/import" className={cn(buttonPrimary, "mt-6 w-fit")}>
            Register a project
          </Link>
        </div>
      ) : null}

      {data && data.items.length > 0 ? (
        <>
          <ul
            aria-busy={isFetching}
            className={cn(
              "mt-10 grid gap-3 sm:grid-cols-2 lg:grid-cols-3",
              isFetching && "opacity-60"
            )}
          >
            {data.items.map((project) => (
              <li key={project.id} className="flex">
                <Link
                  to="/projects/$projectId"
                  params={{ projectId: project.id }}
                  className={cn(
                    panel,
                    "flex flex-1 flex-col hover:border-foreground/50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                  )}
                >
                  <p className={eyebrowItem}>{project.ownerLogin}</p>
                  <h2 className="mt-2 text-lg font-bold tracking-[-0.03em] break-words">
                    {project.name}
                  </h2>
                  <p className="mt-3 line-clamp-3 flex-1 text-sm leading-6 text-muted-foreground">
                    {project.description ?? "No description provided."}
                  </p>
                  <dl className="mt-5 flex items-center gap-4 border-t border-foreground/15 pt-4 text-sm">
                    <div className="flex items-center gap-1.5">
                      <dt className="sr-only">Stars</dt>
                      <Star
                        size={14}
                        aria-hidden="true"
                        className="text-muted-foreground"
                      />
                      <dd className="font-semibold">
                        {new Intl.NumberFormat().format(project.stars)}
                      </dd>
                    </div>
                    {project.language ? (
                      <div className="flex items-center gap-1.5">
                        <dt className="sr-only">Language</dt>
                        <dd className="text-muted-foreground">
                          {project.language}
                        </dd>
                      </div>
                    ) : null}
                  </dl>
                </Link>
              </li>
            ))}
          </ul>

          {totalPages > 1 ? (
            <nav
              className="mt-8 flex flex-wrap items-center justify-between gap-4 border-t border-foreground/15 pt-6"
              aria-label="Project pages"
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
