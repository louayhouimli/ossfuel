package com.example.ossfuel.ui.projects

import com.example.ossfuel.FakeProjectRepository
import com.example.ossfuel.data.ApiException
import com.example.ossfuel.sampleAiUsage
import com.example.ossfuel.sampleApiKey
import com.example.ossfuel.sampleBalance
import com.example.ossfuel.sampleCreatedApiKey
import com.example.ossfuel.sampleProject
import com.example.ossfuel.sampleTransaction
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class ProjectDetailViewModelTest {
    private val dispatcher = UnconfinedTestDispatcher()

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    @Test
    fun loadsTheProjectOnCreation() = runTest {
        val repository = FakeProjectRepository().apply {
            project = sampleProject(name = "ossfuel")
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        val state = viewModel.state.value as ProjectDetailUiState.Content

        assertEquals("ossfuel", state.project.name)
        assertEquals(false, state.deleted)
    }

    @Test
    fun missingProjectBecomesNotFound() = runTest {
        val repository = FakeProjectRepository().apply {
            getError = ApiException(404, "Project not found.")
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")

        assertTrue(viewModel.state.value is ProjectDetailUiState.NotFound)
    }

    @Test
    fun otherLoadFailuresBecomeAnErrorState() = runTest {
        val repository = FakeProjectRepository().apply {
            getError = ApiException(500, "Something broke.")
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        val state = viewModel.state.value as ProjectDetailUiState.Error

        assertEquals("Something broke.", state.message)
    }

    @Test
    fun refreshReplacesTheSnapshot() = runTest {
        val repository = FakeProjectRepository().apply {
            project = sampleProject(stars = 120)
            refreshedProject = sampleProject(stars = 999)
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        viewModel.refresh()

        val state = viewModel.state.value as ProjectDetailUiState.Content
        assertEquals(999, state.project.stars)
        assertEquals(false, state.isRefreshing)
        assertEquals(listOf("project-1"), repository.refreshedIds)
    }

    @Test
    fun failedRefreshKeepsTheSnapshotAndShowsTheError() = runTest {
        val repository = FakeProjectRepository().apply {
            project = sampleProject(stars = 120)
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        repository.refreshError = ApiException(502, "GitHub is unavailable.")

        viewModel.refresh()

        val state = viewModel.state.value as ProjectDetailUiState.Content
        assertEquals(120, state.project.stars)
        assertEquals("GitHub is unavailable.", state.actionError)
        assertEquals(false, state.isRefreshing)
    }

    @Test
    fun deleteMarksTheProjectAsDeleted() = runTest {
        val repository = FakeProjectRepository()

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        viewModel.delete()

        val state = viewModel.state.value as ProjectDetailUiState.Content
        assertEquals(true, state.deleted)
        assertEquals(false, state.isDeleting)
        assertEquals(listOf("project-1"), repository.deletedIds)
    }

    @Test
    fun failedDeleteKeepsTheProjectAndShowsTheError() = runTest {
        val repository = FakeProjectRepository().apply {
            deleteError = ApiException(403, "Only the maintainer can delete this project.")
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        viewModel.delete()

        val state = viewModel.state.value as ProjectDetailUiState.Content
        assertEquals(false, state.deleted)
        assertEquals(false, state.isDeleting)
        assertEquals(
            "Only the maintainer can delete this project.",
            state.actionError,
        )
    }

    @Test
    fun loadsTheBalanceFundingHistoryAndUsageAfterTheProject() = runTest {
        val repository = FakeProjectRepository().apply {
            balance = sampleBalance(amountMinor = 4200)
            transactions = listOf(sampleTransaction())
            aiUsage = listOf(sampleAiUsage())
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")

        val balance = viewModel.balance.value as BalanceUiState.Content
        assertEquals(4200, balance.balance.amountMinor)

        val history = viewModel.history.value as FundingHistoryUiState.Content
        assertEquals(1, history.transactions.size)
        assertEquals(1, history.page)

        val usage = viewModel.aiUsage.value as AiUsageUiState.Content
        assertEquals("gpt-4o-mini", usage.usages.single().model)

        assertEquals(listOf("project-1"), repository.requestedBalanceIds)
        assertEquals(listOf(1 to 10), repository.historyRequests)
        assertEquals(listOf("project-1"), repository.requestedUsageIds)
    }

    @Test
    fun aMissingProjectDoesNotLoadInsights() = runTest {
        val repository = FakeProjectRepository().apply {
            getError = ApiException(404, "Project not found.")
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")

        assertTrue(viewModel.state.value is ProjectDetailUiState.NotFound)
        assertTrue(viewModel.balance.value is BalanceUiState.Loading)
        assertTrue(viewModel.history.value is FundingHistoryUiState.Loading)
        assertTrue(viewModel.aiUsage.value is AiUsageUiState.Loading)
        assertTrue(repository.requestedBalanceIds.isEmpty())
        assertTrue(repository.historyRequests.isEmpty())
        assertTrue(repository.requestedUsageIds.isEmpty())
    }

    @Test
    fun aFailedBalanceLeavesTheProjectVisible() = runTest {
        val repository = FakeProjectRepository().apply {
            balanceError = ApiException(500, "The ledger is unavailable.")
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")

        assertTrue(viewModel.state.value is ProjectDetailUiState.Content)

        val balance = viewModel.balance.value as BalanceUiState.Error
        assertEquals("The ledger is unavailable.", balance.message)
    }

    @Test
    fun fundingHistoryPagesForward() = runTest {
        val repository = FakeProjectRepository().apply {
            transactions = (1..25).map { sampleTransaction(id = "transaction-$it") }
            historyTotalCount = 25
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        viewModel.nextHistoryPage()

        val history = viewModel.history.value as FundingHistoryUiState.Content
        assertEquals(2, history.page)
        assertEquals(3, history.totalPages)
        assertEquals("transaction-11", history.transactions.first().id)
        assertEquals(listOf(1 to 10, 2 to 10), repository.historyRequests)
    }

    @Test
    fun fundingHistoryStopsAtTheFirstPage() = runTest {
        val repository = FakeProjectRepository()

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        viewModel.previousHistoryPage()

        val history = viewModel.history.value as FundingHistoryUiState.Content
        assertEquals(1, history.page)
        assertEquals(listOf(1 to 10), repository.historyRequests)
    }

    @Test
    fun aFailedPageChangeKeepsTheCurrentHistory() = runTest {
        val repository = FakeProjectRepository().apply {
            transactions = (1..25).map { sampleTransaction(id = "transaction-$it") }
            historyTotalCount = 25
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        repository.historyError = ApiException(500, "History unavailable.")

        viewModel.nextHistoryPage()

        val history = viewModel.history.value as FundingHistoryUiState.Content
        assertEquals(1, history.page)
        assertEquals("History unavailable.", history.error)
        assertEquals(false, history.isPaging)
    }

    @Test
    fun aFailedUsageLoadBecomesAnError() = runTest {
        val repository = FakeProjectRepository().apply {
            aiUsageError = ApiException(500, "Usage unavailable.")
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")

        assertTrue(viewModel.state.value is ProjectDetailUiState.Content)

        val usage = viewModel.aiUsage.value as AiUsageUiState.Error
        assertEquals("Usage unavailable.", usage.message)
    }

    @Test
    fun retriesReloadEachInsight() = runTest {
        val repository = FakeProjectRepository().apply {
            balanceError = ApiException(500, "Balance unavailable.")
            historyError = ApiException(500, "History unavailable.")
            aiUsageError = ApiException(500, "Usage unavailable.")
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")

        assertTrue(viewModel.balance.value is BalanceUiState.Error)
        assertTrue(viewModel.history.value is FundingHistoryUiState.Error)
        assertTrue(viewModel.aiUsage.value is AiUsageUiState.Error)

        repository.balanceError = null
        repository.historyError = null
        repository.aiUsageError = null

        viewModel.retryBalance()
        viewModel.retryHistory()
        viewModel.retryAiUsage()

        assertTrue(viewModel.balance.value is BalanceUiState.Content)
        assertTrue(viewModel.history.value is FundingHistoryUiState.Content)
        assertTrue(viewModel.aiUsage.value is AiUsageUiState.Content)
    }

    @Test
    fun apiKeysWaitForTheManagerToOpen() = runTest {
        val repository = FakeProjectRepository().apply {
            apiKeys = listOf(sampleApiKey())
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")

        assertTrue(viewModel.apiKeys.value.isLoading)
        assertTrue(repository.requestedApiKeyProjectIds.isEmpty())

        viewModel.loadApiKeys()

        val state = viewModel.apiKeys.value
        assertEquals(1, state.keys.size)
        assertEquals(false, state.isLoading)
        assertEquals(listOf("project-1"), repository.requestedApiKeyProjectIds)

        viewModel.loadApiKeys()
        assertEquals(1, repository.requestedApiKeyProjectIds.size)
    }

    @Test
    fun aFailedApiKeyLoadCanBeRetried() = runTest {
        val repository = FakeProjectRepository().apply {
            apiKeysError = ApiException(500, "Keys unavailable.")
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        viewModel.loadApiKeys()

        assertEquals("Keys unavailable.", viewModel.apiKeys.value.loadError)

        repository.apiKeysError = null
        viewModel.retryApiKeys()

        assertEquals(null, viewModel.apiKeys.value.loadError)
        assertEquals(1, viewModel.apiKeys.value.keys.size)
    }

    @Test
    fun creatingAKeyKeepsTheRawKeyAndPrependsIt() = runTest {
        val repository = FakeProjectRepository().apply {
            apiKeys = listOf(sampleApiKey(id = "key-old", name = "Old key"))
            createdApiKey = sampleCreatedApiKey(
                id = "key-new",
                name = "New key",
                rawKey = "ossf_live_xyz",
            )
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        viewModel.loadApiKeys()
        viewModel.setApiKeyName("New key")
        viewModel.createApiKey()

        val state = viewModel.apiKeys.value
        assertEquals("ossf_live_xyz", state.createdKey?.rawKey)
        assertEquals("", state.draftName)
        assertEquals(listOf("key-new", "key-old"), state.keys.map { it.id })
        assertEquals(listOf("project-1" to "New key"), repository.createdApiKeyRequests)
    }

    @Test
    fun blankKeyNamesAreIgnored() = runTest {
        val repository = FakeProjectRepository()

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        viewModel.setApiKeyName("   ")
        viewModel.createApiKey()

        assertTrue(repository.createdApiKeyRequests.isEmpty())
        assertEquals("   ", viewModel.apiKeys.value.draftName)
    }

    @Test
    fun keyNamesAreTrimmedAndLimited() = runTest {
        val repository = FakeProjectRepository()

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        viewModel.setApiKeyName("x".repeat(150))

        assertEquals(100, viewModel.apiKeys.value.draftName.length)

        viewModel.setApiKeyName("  Aider CLI  ")
        viewModel.createApiKey()

        assertEquals(
            listOf("project-1" to "Aider CLI"),
            repository.createdApiKeyRequests,
        )
    }

    @Test
    fun aFailedCreateKeepsTheDraft() = runTest {
        val repository = FakeProjectRepository().apply {
            createApiKeyError = ApiException(400, "Name is invalid.")
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        viewModel.loadApiKeys()
        viewModel.setApiKeyName("Aider CLI")
        viewModel.createApiKey()

        val state = viewModel.apiKeys.value
        assertEquals("Aider CLI", state.draftName)
        assertEquals("Name is invalid.", state.createError)
        assertEquals(null, state.createdKey)
        assertEquals(false, state.isCreating)
    }

    @Test
    fun dismissingTheCreatedKeyClearsIt() = runTest {
        val repository = FakeProjectRepository()

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        viewModel.loadApiKeys()
        viewModel.setApiKeyName("Aider CLI")
        viewModel.createApiKey()
        viewModel.dismissCreatedApiKey()

        assertEquals(null, viewModel.apiKeys.value.createdKey)
    }

    @Test
    fun revokingMarksOnlyThatKeyInactive() = runTest {
        val repository = FakeProjectRepository().apply {
            apiKeys = listOf(
                sampleApiKey(id = "key-1", isActive = true),
                sampleApiKey(id = "key-2", name = "Other", isActive = true),
            )
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        viewModel.loadApiKeys()
        viewModel.revokeApiKey("key-1")

        val state = viewModel.apiKeys.value
        assertEquals(false, state.keys.first { it.id == "key-1" }.isActive)
        assertEquals(true, state.keys.first { it.id == "key-2" }.isActive)
        assertEquals(null, state.revokingKeyId)
        assertEquals(listOf("project-1" to "key-1"), repository.revokedApiKeyRequests)
    }

    @Test
    fun aFailedRevokeKeepsTheKeyActive() = runTest {
        val repository = FakeProjectRepository().apply {
            apiKeys = listOf(sampleApiKey(id = "key-1", isActive = true))
            revokeApiKeyError = ApiException(500, "Could not revoke.")
        }

        val viewModel = ProjectDetailViewModel(repository, "project-1")
        viewModel.loadApiKeys()
        viewModel.revokeApiKey("key-1")

        val state = viewModel.apiKeys.value
        assertEquals(true, state.keys.single().isActive)
        assertEquals("Could not revoke.", state.revokeError)
        assertEquals(null, state.revokingKeyId)
    }
}
