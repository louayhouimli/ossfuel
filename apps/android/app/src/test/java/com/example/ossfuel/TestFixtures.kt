package com.example.ossfuel

import com.example.ossfuel.data.ApiException
import com.example.ossfuel.data.AvailableRepository
import com.example.ossfuel.data.Project
import com.example.ossfuel.data.ProjectList
import com.example.ossfuel.data.ProjectRepository
import com.example.ossfuel.data.ProjectSummary

fun sampleProject(
    id: String = "project-1",
    name: String = "ossfuel",
    ownerLogin: String = "octocat",
    ownerUserId: String = "user-1",
    stars: Int = 120,
    topics: List<String> = listOf("funding", "ai"),
) = Project(
    id = id,
    fullName = "$ownerLogin/$name",
    ownerLogin = ownerLogin,
    name = name,
    description = "An accounting layer for open-source AI spend.",
    htmlUrl = "https://github.com/$ownerLogin/$name",
    homepageUrl = null,
    language = "Kotlin",
    stars = stars,
    forks = 8,
    openIssues = 3,
    defaultBranch = "main",
    topics = topics,
    createdAt = "2025-06-01T12:00:00Z",
    lastSyncedAt = "2026-02-01T12:00:00Z",
    ownerUserId = ownerUserId,
)

fun sampleSummary(
    id: String = "project-1",
    name: String = "ossfuel",
    ownerLogin: String = "octocat",
) = ProjectSummary(
    id = id,
    fullName = "$ownerLogin/$name",
    ownerLogin = ownerLogin,
    name = name,
    description = "An accounting layer for open-source AI spend.",
    language = "Kotlin",
    stars = 120,
)

fun sampleRepository(
    id: Long = 1L,
    name: String = "ossfuel",
    ownerLogin: String = "octocat",
    alreadyImported: Boolean = false,
) = AvailableRepository(
    id = id,
    fullName = "$ownerLogin/$name",
    ownerLogin = ownerLogin,
    name = name,
    description = "An accounting layer for open-source AI spend.",
    language = "Kotlin",
    stars = 120,
    alreadyImported = alreadyImported,
)

/**
 * An in-memory [ProjectRepository] whose results and failures are settable per
 * test. It records calls so paging and import behaviour can be asserted.
 */
class FakeProjectRepository : ProjectRepository {
    var projects: List<ProjectSummary> = emptyList()
    var totalCount: Int = 0
    var project: Project = sampleProject()
    var available: List<AvailableRepository> = emptyList()
    var refreshedProject: Project = sampleProject()
    var importedProject: Project = sampleProject()

    var listError: ApiException? = null
    var getError: ApiException? = null
    var availableError: ApiException? = null
    var importError: ApiException? = null
    var refreshError: ApiException? = null
    var deleteError: ApiException? = null

    val listedPages = mutableListOf<Int>()
    val refreshedIds = mutableListOf<String>()
    val importedFullNames = mutableListOf<String>()
    val deletedIds = mutableListOf<String>()

    override suspend fun list(page: Int, pageSize: Int): ProjectList {
        listError?.let { throw it }

        listedPages += page
        val start = (page - 1) * pageSize

        return ProjectList(
            items = projects.drop(start).take(pageSize),
            page = page,
            pageSize = pageSize,
            totalCount = totalCount,
        )
    }

    override suspend fun get(id: String): Project {
        getError?.let { throw it }

        return project
    }

    override suspend fun availableRepositories(): List<AvailableRepository> {
        availableError?.let { throw it }

        return available
    }

    override suspend fun importProject(fullName: String): Project {
        importError?.let { throw it }

        importedFullNames += fullName

        return importedProject
    }

    override suspend fun refresh(id: String): Project {
        refreshError?.let { throw it }

        refreshedIds += id

        return refreshedProject
    }

    override suspend fun delete(id: String) {
        deleteError?.let { throw it }

        deletedIds += id
    }
}
