import { createFileRoute, Link, useNavigate } from "@tanstack/react-router"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Star } from "lucide-react"
import { Loader } from "@/components/loader"
import { PageHeader } from "@/components/page-header"
import { ApiError, API_URL } from "@/lib/api"
import { availableRepositoriesQueryOptions, importProject } from "@/lib/projects"
import {
  buttonPrimary,
  buttonPrimaryCompact,
  eyebrowItem,
  panel,
} from "@/lib/styles"
import { cn } from "@workspace/ui/lib/utils"

export const Route = createFileRoute("/_site/_authenticated/import")({
  head: () => ({
    meta: [
      { title: "Import a repository | OSSFuel" },
      {
        name: "description",
        content:
          "Choose one of your public GitHub repositories to register as a sponsorable project on OSSFuel.",
      },
    ],
  }),
  component: ImportPage,
})

function ImportPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const {
    data: repositories,
    isLoading,
    error,
  } = useQuery(availableRepositoriesQueryOptions())

  const needsReconnect = error instanceof ApiError && error.status === 403

  const importMutation = useMutation({
    mutationFn: (fullName: string) => importProject(fullName),
    onSuccess: async (project) => {
      await queryClient.invalidateQueries({ queryKey: ["projects"] })
      await queryClient.invalidateQueries({ queryKey: ["repositories"] })
      await navigate({
        to: "/projects/$projectId",
        params: { projectId: project.id },
      })
    },
  })

  const failedRepository =
    importMutation.isError && typeof importMutation.variables === "string"
      ? importMutation.variables
      : null

  return (
    <main className="mx-auto max-w-6xl px-5 py-8 sm:px-8 sm:py-12">
      <PageHeader
        eyebrow="Register a project"
        title="Import a repository"
        description="Choose one of your public GitHub repositories. OSSFuel retrieves its information from GitHub and registers it as a project. One repository becomes one project."
      />

      {isLoading ? (
        <Loader
          label="Loading your repositories from GitHub"
          className="mt-8"
        />
      ) : null}

      {needsReconnect ? (
        <div className={cn(panel, "mt-8")}>
          <h2 className="font-sans text-lg font-bold">Reconnect GitHub</h2>
          <p className="mt-2 max-w-xl leading-7 text-muted-foreground">
            OSSFuel needs permission to read your repositories. Sign in again to
            grant it.
          </p>
          <a
            href={`${API_URL}/login?returnUrl=${encodeURIComponent("/import")}`}
            className={cn(buttonPrimary, "mt-5 w-fit")}
          >
            Reconnect GitHub
          </a>
        </div>
      ) : null}

      {error && !needsReconnect ? (
        <p
          role="alert"
          className="mt-8 border border-destructive/40 bg-destructive/10 px-5 py-4 text-sm text-foreground"
        >
          OSSFuel could not read your repositories from GitHub. Try again
          shortly.
        </p>
      ) : null}

      {repositories && repositories.length === 0 ? (
        <div className={cn(panel, "mt-8")}>
          <h2 className="font-sans text-lg font-bold">
            No repositories are available to import
          </h2>
          <p className="mt-2 max-w-xl leading-7 text-muted-foreground">
            OSSFuel imports public repositories you can push to. Create one on
            GitHub, then come back.
          </p>
        </div>
      ) : null}

      {repositories && repositories.length > 0 ? (
        <ul className="mt-10 border-t border-foreground/15">
          {repositories.map((repository) => {
            const failed = failedRepository === repository.fullName
            const importing =
              importMutation.isPending &&
              importMutation.variables === repository.fullName

            return (
              <li
                key={repository.id}
                className="border-b border-foreground/15 py-6"
              >
                <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                  <div className="min-w-0">
                    <p className={eyebrowItem}>{repository.ownerLogin}</p>
                    <h2 className="mt-2 text-lg font-bold tracking-[-0.03em] break-words">
                      {repository.name}
                    </h2>
                    <p className="mt-2 line-clamp-2 max-w-2xl text-sm leading-6 text-muted-foreground">
                      {repository.description ?? "No description provided."}
                    </p>
                    <div className="mt-3 flex items-center gap-3 text-sm text-muted-foreground">
                      <span className="inline-flex items-center gap-1.5">
                        <Star size={14} aria-hidden="true" />
                        {new Intl.NumberFormat().format(repository.stars)}
                      </span>
                      {repository.language ? (
                        <span>{repository.language}</span>
                      ) : null}
                    </div>
                  </div>

                  <div className="shrink-0">
                    {repository.alreadyImported ? (
                      <span className="inline-flex min-h-11 items-center text-sm font-semibold text-muted-foreground">
                        Already registered
                      </span>
                    ) : (
                      <button
                        type="button"
                        onClick={() =>
                          importMutation.mutate(repository.fullName)
                        }
                        disabled={importMutation.isPending}
                        className={cn(
                          buttonPrimaryCompact,
                          "disabled:opacity-50"
                        )}
                      >
                        {importing ? "Registering" : "Register project"}
                      </button>
                    )}
                  </div>
                </div>

                {failed ? (
                  <p
                    role="alert"
                    className="mt-4 border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-foreground"
                  >
                    {importMutation.error instanceof ApiError
                      ? importMutation.error.message
                      : "The project could not be registered. Try again."}
                  </p>
                ) : null}
              </li>
            )
          })}
        </ul>
      ) : null}

      <p className="mt-8 text-sm text-muted-foreground">
        <Link
          to="/projects"
          className="font-semibold underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
        >
          Browse projects
        </Link>{" "}
        to see what other maintainers have registered.
      </p>
    </main>
  )
}
