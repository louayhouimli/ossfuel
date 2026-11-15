package com.example.ossfuel.ui.projects

import com.example.ossfuel.FakeProjectRepository
import com.example.ossfuel.data.ApiException
import com.example.ossfuel.sampleSummary
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
class ProjectsViewModelTest {
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
    fun initialLoadShowsTheFirstPage() = runTest {
        val repository = FakeProjectRepository().apply {
            projects = listOf(sampleSummary(id = "a"), sampleSummary(id = "b"))
            totalCount = 2
        }

        val viewModel = ProjectsViewModel(repository)
        val state = viewModel.state.value as ProjectsUiState.Content

        assertEquals(listOf("a", "b"), state.projects.map { it.id })
        assertEquals(1, state.page)
        assertEquals(1, state.totalPages)
        assertEquals(listOf(1), repository.listedPages)
    }

    @Test
    fun emptyResultIsContentRatherThanAnError() = runTest {
        val repository = FakeProjectRepository()

        val viewModel = ProjectsViewModel(repository)
        val state = viewModel.state.value as ProjectsUiState.Content

        assertTrue(state.projects.isEmpty())
        assertEquals(1, state.totalPages)
    }

    @Test
    fun failedFirstLoadShowsTheApiMessage() = runTest {
        val repository = FakeProjectRepository().apply {
            listError = ApiException(500, "The API is down.")
        }

        val viewModel = ProjectsViewModel(repository)
        val state = viewModel.state.value as ProjectsUiState.Error

        assertEquals("The API is down.", state.message)
    }

    @Test
    fun failedRefreshKeepsTheCurrentPageAndSurfacesTheError() = runTest {
        val repository = FakeProjectRepository().apply {
            projects = listOf(sampleSummary())
            totalCount = 1
        }

        val viewModel = ProjectsViewModel(repository)
        repository.listError = ApiException(500, "Refresh failed.")

        viewModel.refresh()

        val state = viewModel.state.value as ProjectsUiState.Content
        assertEquals(1, state.projects.size)
        assertEquals("Refresh failed.", state.error)
        assertEquals(false, state.isRefreshing)
    }

    @Test
    fun nextAndPreviousPageStayWithinTheResultSet() = runTest {
        val repository = FakeProjectRepository().apply {
            projects = (1..25).map { sampleSummary(id = "project-$it") }
            totalCount = 25
        }

        val viewModel = ProjectsViewModel(repository)
        assertEquals(
            1,
            (viewModel.state.value as ProjectsUiState.Content).page,
        )

        viewModel.nextPage()
        assertEquals(
            2,
            (viewModel.state.value as ProjectsUiState.Content).page,
        )

        viewModel.nextPage()
        assertEquals(
            3,
            (viewModel.state.value as ProjectsUiState.Content).page,
        )

        viewModel.nextPage()
        assertEquals(
            3,
            (viewModel.state.value as ProjectsUiState.Content).page,
        )

        viewModel.previousPage()
        assertEquals(
            2,
            (viewModel.state.value as ProjectsUiState.Content).page,
        )

        assertEquals(listOf(1, 2, 3, 2), repository.listedPages)
    }

    @Test
    fun retryReloadsAfterAFailedFirstLoad() = runTest {
        val repository = FakeProjectRepository().apply {
            listError = ApiException(0, "Can't reach OSSFuel.")
        }

        val viewModel = ProjectsViewModel(repository)
        assertTrue(viewModel.state.value is ProjectsUiState.Error)

        repository.listError = null
        repository.projects = listOf(sampleSummary())
        repository.totalCount = 1

        viewModel.retry()

        val state = viewModel.state.value as ProjectsUiState.Content
        assertEquals(1, state.projects.size)
    }
}
