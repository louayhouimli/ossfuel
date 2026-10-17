import { keepPreviousData, queryOptions } from "@tanstack/react-query"
import { apiFetch } from "./api"

export type ProjectSummary = {
  id: string
  fullName: string
  ownerLogin: string
  name: string
  description: string | null
  language: string | null
  stars: number
  topics: string[]
}

export type Project = ProjectSummary & {
  htmlUrl: string
  homepageUrl: string | null
  forks: number
  openIssues: number
  defaultBranch: string | null
  createdAt: string
  lastSyncedAt: string
  ownerUserId: string
}

export type ProjectList = {
  items: ProjectSummary[]
  page: number
  pageSize: number
  totalCount: number
}

export type AvailableRepository = {
  id: number
  fullName: string
  ownerLogin: string
  name: string
  description: string | null
  language: string | null
  stars: number
  alreadyImported: boolean
}

export const projectsQueryOptions = (page: number, pageSize: number) =>
  queryOptions({
    queryKey: ["projects", { page, pageSize }],
    queryFn: () => apiFetch<ProjectList>(`/projects?page=${page}&pageSize=${pageSize}`),
    placeholderData: keepPreviousData,
  })

export const projectQueryOptions = (id: string) =>
  queryOptions({
    queryKey: ["projects", id],
    queryFn: () => apiFetch<Project>(`/projects/${id}`),
    // The detail route awaits this in a loader, so a missing project must fail
    // on the first attempt rather than block navigation through retries.
    retry: false,
  })

export const availableRepositoriesQueryOptions = () =>
  queryOptions({
    queryKey: ["repositories", "available"],
    queryFn: () => apiFetch<AvailableRepository[]>("/repos/available"),
    retry: false,
  })

export function importProject(fullName: string): Promise<Project> {
  return apiFetch<Project>("/projects", {
    method: "POST",
    body: JSON.stringify({ fullName }),
  })
}

export function refreshProject(id: string): Promise<Project> {
  return apiFetch<Project>(`/projects/${id}/refresh`, { method: "POST" })
}

export function deleteProject(id: string): Promise<void> {
  return apiFetch<void>(`/projects/${id}`, { method: "DELETE" })
}
