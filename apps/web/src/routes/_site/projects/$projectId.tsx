import { useState, type ReactNode } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { createFileRoute, Link, useRouter } from "@tanstack/react-router"
import {
  ArrowLeft,
  Calendar,
  CircleDot,
  Clock,
  Code,
  ExternalLink,
  FolderGit2,
  GitBranch,
  GitFork,
  Globe,
  RefreshCw,
  Star,
  Tag,
  Trash2,
} from "lucide-react"
import { Loader } from "@/components/loader"
import { BalancePanel } from "@/components/balance-panel"
import { FundProjectForm } from "@/components/fund-project-form"
import { LiveTime } from "@/components/live-time"
import { PageHeader } from "@/components/page-header"
import { ApiKeysManager } from "@/components/api-keys-manager"
import { AIUsageAudit } from "@/components/ai-usage-audit"
import { authQueryOptions } from "@/lib/auth"
import { ApiError } from "@/lib/api"
import {
  deleteProject,
  projectQueryOptions,
  refreshProject,
} from "@/lib/projects"
import {
  buttonOutline,
  buttonOutlineCompact,
  buttonPrimary,
  buttonPrimaryCompact,
  eyebrowItem,
  panel,
} from "@/lib/styles"
import { cn } from "@workspace/ui/lib/utils"

export const Route = createFileRoute("/_site/projects/$projectId")({
  // The loader exists so `head` can title the page with the real project name.
  // It fills the same react-query cache the page reads, so nothing is fetched
  // twice. Failures are swallowed here so the page keeps its own not-found and
  // error treatments rather than a route-level error boundary; the page reads
  // the error out of the query cache to tell the two apart.
  loader: async ({ params, context }) => {
    try {
      const project = await context.queryClient.ensureQueryData(
        projectQueryOptions(params.projectId)
      )

      return { project, failed: false as const }
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        return { project: null, failed: false as const }
      }

      return { project: null, failed: true as const }
    }
  },
  head: ({ loaderData }) => {
    const project = loaderData?.project

    if (project) {
      return {
        meta: [
          { title: `${project.name} | OSSFuel` },
          {
            name: "description",
            content:
              project.description ??
              `${project.ownerLogin}/${project.name} on OSSFuel: repository details and funding status.`,
          },
        ],
      }
    }

    return {
      meta: [
        {
          title: loaderData?.failed
            ? "Project | OSSFuel"
            : "Project not found | OSSFuel",
        },
        {
          name: "description",
          content: "Repository details for an open-source project on OSSFuel.",
        },
      ],
    }
  },
  pendingMs: 200,
  pendingComponent: ProjectPending,
  component: ProjectDetailPage,
})

function ProjectPending() {
  return (
    <main className="mx-auto max-w-6xl px-5 py-8 sm:px-8 sm:py-12">
      <Loader label="Loading project" />
    </main>
  )
}

function ProjectDetailPage() {
  const { projectId } = Route.useParams()
  const router = useRouter()
  const queryClient = useQueryClient()
  const {
    data: project,
    error,
    isError,
  } = useQuery(projectQueryOptions(projectId))
  const { data: user } = useQuery(authQueryOptions())
  const [confirmingDelete, setConfirmingDelete] = useState(false)

  const refresh = useMutation({
    mutationFn: () => refreshProject(projectId),
    onSuccess: async (updated) => {
      queryClient.setQueryData(projectQueryOptions(projectId).queryKey, updated)
      await router.invalidate()
    },
  })

  const remove = useMutation({
    mutationFn: () => deleteProject(projectId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["projects"] })
      await router.navigate({ to: "/projects" })
    },
  })

  const notFound = isError && error instanceof ApiError && error.status === 404
  const canManage =
    project != null && user != null && user.id === project.ownerUserId

  return (
    <main className="mx-auto max-w-6xl px-5 py-8 sm:px-8 sm:py-12">
      <Link
        to="/projects"
        className="inline-flex min-h-11 items-center gap-2 rounded-sm text-sm font-semibold text-muted-foreground hover:text-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        <ArrowLeft size={16} aria-hidden="true" />
        All projects
      </Link>

      {notFound ? (
        <div className={cn(panel, "mt-8")}>
          <h1 className="text-2xl font-bold tracking-[-0.03em]">
            Project not found
          </h1>
          <p className="mt-3 text-muted-foreground">
            This project does not exist or has been removed.
          </p>
        </div>
      ) : null}

      {isError && !notFound ? (
        <p className="mt-8 text-muted-foreground" role="alert">
          The project could not be loaded. Try again shortly.
        </p>
      ) : null}

      {project ? (
        <article className="mt-5">
          <PageHeader
            eyebrow={project.ownerLogin}
            title={project.name}
            description={project.description ?? "No description provided."}
          />

          <div className="mt-6 flex flex-col gap-3 sm:flex-row">
            <a href="#fund" className={cn(buttonPrimary, "sm:w-fit")}>
              Fund this project
            </a>
            <a
              href={project.htmlUrl}
              target="_blank"
              rel="noreferrer"
              className={cn(buttonOutline, "sm:w-fit")}
            >
              View on GitHub
              <ExternalLink size={16} aria-hidden="true" />
            </a>
          </div>

          <section
            id="fund"
            className="mt-10 scroll-mt-24"
            aria-label="Project funding"
          >
            <div className="grid items-start gap-3 lg:grid-cols-[1.4fr_1fr]">
              <BalancePanel projectId={projectId} />

              {user ? (
                <FundProjectForm projectId={projectId} />
              ) : (
                <div className={panel}>
                  <p className={eyebrowItem}>Fund this project</p>
                  <h2 className="mt-3 text-xl font-bold tracking-[-0.03em]">
                    Add to the AI Balance
                  </h2>
                  <p className="mt-3 text-sm leading-6 text-muted-foreground">
                    Sign in with GitHub to fund this project. Your own funding
                    history stays private to you.
                  </p>
                  <Link
                    to="/sign-in"
                    className={cn(buttonPrimary, "mt-5 w-full")}
                  >
                    Sign in to fund
                  </Link>
                </div>
              )}
            </div>
          </section>

          <AIUsageAudit projectId={project.id} />

          <section className="mt-12" aria-labelledby="record-heading">
            <h2 id="record-heading" className={eyebrowItem}>
              Repository record
            </h2>

            <dl className="mt-4 border-t border-foreground/15">
              <RecordRow
                label="Full name"
                icon={<FolderGit2 size={14} aria-hidden="true" />}
              >
                <a
                  href={project.htmlUrl}
                  className="underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
                >
                  {project.fullName}
                </a>
              </RecordRow>

              <RecordRow
                label="Language"
                icon={<Code size={14} aria-hidden="true" />}
              >
                {project.language ?? "Not specified"}
              </RecordRow>

              <RecordRow
                label="Stars"
                icon={<Star size={14} aria-hidden="true" />}
              >
                <span className="tabular-nums">
                  {numberFormat.format(project.stars)}
                </span>
              </RecordRow>

              <RecordRow
                label="Forks"
                icon={<GitFork size={14} aria-hidden="true" />}
              >
                <span className="tabular-nums">
                  {numberFormat.format(project.forks)}
                </span>
              </RecordRow>

              <RecordRow
                label="Open issues"
                icon={<CircleDot size={14} aria-hidden="true" />}
              >
                <span className="tabular-nums">
                  {numberFormat.format(project.openIssues)}
                </span>
              </RecordRow>

              <RecordRow
                label="Default branch"
                icon={<GitBranch size={14} aria-hidden="true" />}
              >
                {project.defaultBranch ?? "Unknown"}
              </RecordRow>

              {project.homepageUrl ? (
                <RecordRow
                  label="Website"
                  icon={<Globe size={14} aria-hidden="true" />}
                >
                  <a
                    href={project.homepageUrl}
                    className="underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
                  >
                    {project.homepageUrl}
                  </a>
                </RecordRow>
              ) : null}

              <RecordRow
                label="Registered"
                icon={<Calendar size={14} aria-hidden="true" />}
              >
                {formatDate(project.createdAt)}
              </RecordRow>

              <RecordRow
                label="Last snapshot"
                icon={<Clock size={14} aria-hidden="true" />}
              >
                <LiveTime value={project.lastSyncedAt} />
              </RecordRow>

              {project.topics.length > 0 ? (
                <RecordRow
                  label="Topics"
                  icon={<Tag size={14} aria-hidden="true" />}
                >
                  <ul className="flex flex-wrap gap-2">
                    {project.topics.map((topic) => (
                      <li
                        key={topic}
                        className="border border-foreground/20 px-2.5 py-1 text-xs font-medium"
                      >
                        {topic}
                      </li>
                    ))}
                  </ul>
                </RecordRow>
              ) : null}
            </dl>
          </section>

          {canManage ? (
            <section className="mt-10 border-t border-foreground/15 pt-8">
              <h2 className={eyebrowItem}>Maintainer</h2>
              <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground">
                You registered this project. Refreshing re-reads the numbers
                from GitHub; deleting removes it from OSSFuel and leaves the
                repository itself untouched.
              </p>

              <ApiKeysManager projectId={project.id} />

              <div className="mt-8 flex flex-col gap-3 sm:flex-row">
                <button
                  type="button"
                  onClick={() => refresh.mutate()}
                  disabled={refresh.isPending}
                  className={cn(
                    buttonOutlineCompact,
                    "disabled:opacity-60 sm:w-fit"
                  )}
                >
                  <RefreshCw
                    size={16}
                    aria-hidden="true"
                    className={refresh.isPending ? "animate-spin" : undefined}
                  />
                  {refresh.isPending ? "Refreshing" : "Refresh from GitHub"}
                </button>

                <button
                  type="button"
                  onClick={() => {
                    remove.reset()
                    setConfirmingDelete(true)
                  }}
                  className={cn(buttonOutlineCompact, "sm:w-fit")}
                >
                  <Trash2 size={16} aria-hidden="true" />
                  Delete project
                </button>
              </div>

              {refresh.isError ? (
                <p className="mt-4 text-sm font-semibold" role="alert">
                  {errorMessage(refresh.error)}
                </p>
              ) : null}

              {confirmingDelete ? (
                <div className={cn(panel, "mt-5")} role="alert">
                  <h3 className="text-lg font-bold tracking-[-0.03em]">
                    Delete this project?
                  </h3>
                  <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground">
                    This removes {project.fullName} from OSSFuel. The GitHub
                    repository is not touched, and you can register it again
                    later.
                  </p>

                  <div className="mt-5 flex flex-col gap-3 sm:flex-row">
                    <button
                      type="button"
                      onClick={() => remove.mutate()}
                      disabled={remove.isPending}
                      className={cn(
                        buttonPrimaryCompact,
                        "disabled:opacity-60 sm:w-fit"
                      )}
                    >
                      {remove.isPending ? "Deleting" : "Yes, delete it"}
                    </button>
                    <button
                      type="button"
                      onClick={() => setConfirmingDelete(false)}
                      className={cn(buttonOutlineCompact, "sm:w-fit")}
                    >
                      Keep it
                    </button>
                  </div>

                  {remove.isError ? (
                    <p className="mt-4 text-sm font-semibold" role="alert">
                      {errorMessage(remove.error)}
                    </p>
                  ) : null}
                </div>
              ) : null}
            </section>
          ) : null}
        </article>
      ) : null}
    </main>
  )
}

const numberFormat = new Intl.NumberFormat()

function RecordRow({
  label,
  icon,
  children,
}: {
  label: string
  icon: ReactNode
  children: ReactNode
}) {
  return (
    <div className="grid grid-cols-[7rem_minmax(0,1fr)] items-baseline gap-x-4 border-b border-foreground/15 py-3.5 sm:grid-cols-[12rem_minmax(0,1fr)]">
      <dt className={cn("flex items-center gap-2 leading-5", eyebrowItem)}>
        <span className="shrink-0 text-muted-foreground" aria-hidden="true">
          {icon}
        </span>
        {label}
      </dt>
      <dd className="min-w-0 text-sm leading-5 font-semibold break-words">
        {children}
      </dd>
    </div>
  )
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(
    new Date(value)
  )
}

function errorMessage(error: unknown) {
  return error instanceof ApiError
    ? error.message
    : "Something went wrong. Try again."
}
