package com.example.ossfuel

import com.example.ossfuel.data.AiUsage
import com.example.ossfuel.data.ApiException
import com.example.ossfuel.data.AvailableRepository
import com.example.ossfuel.data.Balance
import com.example.ossfuel.data.BalanceTransaction
import com.example.ossfuel.data.BalanceTransactionList
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

fun sampleBalance(
    projectId: String = "project-1",
    amountMinor: Long = 1250,
    currency: String = "usd",
) = Balance(
    projectId = projectId,
    amountMinor = amountMinor,
    currency = currency,
)

fun sampleTransaction(
    id: String = "transaction-1",
    amountMinor: Long = 1250,
    type: String = "funding_credit",
    description: String? = "Sponsor credit",
    createdAt: String = "2026-02-01T12:00:00Z",
) = BalanceTransaction(
    id = id,
    amountMinor = amountMinor,
    type = type,
    status = "succeeded",
    description = description,
    sponsor = null,
    createdAt = createdAt,
)

fun sampleAiUsage(
    id: String = "usage-1",
    model: String = "gpt-4o-mini",
    provider: String = "openai",
    promptTokens: Int = 1_234,
    completionTokens: Int = 567,
    costMinor: Long = 42,
    createdAt: String = "2026-02-01T12:00:00Z",
) = AiUsage(
    id = id,
    projectId = "project-1",
    apiKeyId = "key-1",
    model = model,
    provider = provider,
    promptTokens = promptTokens,
    completionTokens = completionTokens,
    totalTokens = promptTokens + completionTokens,
    costMinor = costMinor,
    status = "succeeded",
    createdAt = createdAt,
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
    var balance: Balance = sampleBalance()
    var transactions: List<BalanceTransaction> = listOf(sampleTransaction())
    var historyTotalCount: Int = 1
    var aiUsage: List<AiUsage> = listOf(sampleAiUsage())

    var listError: ApiException? = null
    var getError: ApiException? = null
    var availableError: ApiException? = null
    var importError: ApiException? = null
    var refreshError: ApiException? = null
    var deleteError: ApiException? = null
    var balanceError: ApiException? = null
    var historyError: ApiException? = null
    var aiUsageError: ApiException? = null

    val listedPages = mutableListOf<Int>()
    val refreshedIds = mutableListOf<String>()
    val importedFullNames = mutableListOf<String>()
    val deletedIds = mutableListOf<String>()
    val requestedBalanceIds = mutableListOf<String>()
    val historyRequests = mutableListOf<Pair<Int, Int>>()
    val requestedUsageIds = mutableListOf<String>()

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

    override suspend fun balance(projectId: String): Balance {
        balanceError?.let { throw it }

        requestedBalanceIds += projectId

        return balance
    }

    override suspend fun balanceTransactions(
        projectId: String,
        page: Int,
        pageSize: Int,
    ): BalanceTransactionList {
        historyError?.let { throw it }

        historyRequests += page to pageSize
        val start = (page - 1) * pageSize

        return BalanceTransactionList(
            items = transactions.drop(start).take(pageSize),
            page = page,
            pageSize = pageSize,
            totalCount = historyTotalCount,
        )
    }

    override suspend fun aiUsage(projectId: String): List<AiUsage> {
        aiUsageError?.let { throw it }

        requestedUsageIds += projectId

        return aiUsage
    }
}
