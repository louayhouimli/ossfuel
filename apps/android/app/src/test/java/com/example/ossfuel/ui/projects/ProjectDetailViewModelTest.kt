package com.example.ossfuel.ui.projects

import com.example.ossfuel.FakeProjectRepository
import com.example.ossfuel.data.ApiException
import com.example.ossfuel.sampleProject
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
}
