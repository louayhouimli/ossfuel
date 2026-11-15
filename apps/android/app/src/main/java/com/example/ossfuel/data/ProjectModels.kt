package com.example.ossfuel.data

import kotlinx.serialization.Serializable

@Serializable
data class ProjectSummary(
    val id: String,
    val fullName: String,
    val ownerLogin: String,
    val name: String,
    val description: String? = null,
    val language: String? = null,
    val stars: Int = 0,
    val topics: List<String> = emptyList(),
)

@Serializable
data class ProjectList(
    val items: List<ProjectSummary> = emptyList(),
    val page: Int = 1,
    val pageSize: Int = 12,
    val totalCount: Int = 0,
) {
    val totalPages: Int
        get() = if (pageSize <= 0) 1 else maxOf(1, (totalCount + pageSize - 1) / pageSize)
}

@Serializable
data class Project(
    val id: String,
    val fullName: String,
    val ownerLogin: String,
    val name: String,
    val description: String? = null,
    val htmlUrl: String,
    val homepageUrl: String? = null,
    val language: String? = null,
    val stars: Int = 0,
    val forks: Int = 0,
    val openIssues: Int = 0,
    val defaultBranch: String? = null,
    val topics: List<String> = emptyList(),
    val createdAt: String,
    val lastSyncedAt: String,
    val ownerUserId: String,
)

@Serializable
data class AvailableRepository(
    val id: Long,
    val fullName: String,
    val ownerLogin: String,
    val name: String,
    val description: String? = null,
    val language: String? = null,
    val stars: Int = 0,
    val alreadyImported: Boolean = false,
)

@Serializable
data class CreateProjectRequest(
    val fullName: String,
)
