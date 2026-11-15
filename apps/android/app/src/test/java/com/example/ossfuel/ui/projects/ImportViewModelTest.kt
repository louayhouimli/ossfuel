package com.example.ossfuel.ui.projects

import com.example.ossfuel.FakeProjectRepository
import com.example.ossfuel.data.ApiException
import com.example.ossfuel.sampleProject
import com.example.ossfuel.sampleRepository
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class ImportViewModelTest {
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
    fun loadsAvailableRepositories() = runTest {
        val repository = FakeProjectRepository().apply {
            available = listOf(
                sampleRepository(id = 1L, name = "alpha"),
                sampleRepository(id = 2L, name = "beta"),
            )
        }

        val viewModel = ImportViewModel(repository)
        val state = viewModel.state.value as ImportUiState.Content

        assertEquals(2, state.repositories.size)
        assertEquals(2, state.filteredRepositories.size)
    }

    @Test
    fun failedLoadShowsTheApiMessage() = runTest {
        val repository = FakeProjectRepository().apply {
            availableError = ApiException(403, "Reconnect GitHub.")
        }

        val viewModel = ImportViewModel(repository)
        val state = viewModel.state.value as ImportUiState.Error

        assertEquals("Reconnect GitHub.", state.message)
    }

    @Test
    fun queryFiltersByNameAndDescriptionIgnoringCase() = runTest {
        val repository = FakeProjectRepository().apply {
            available = listOf(
                sampleRepository(id = 1L, name = "ossfuel"),
                sampleRepository(id = 2L, name = "website"),
            )
        }

        val viewModel = ImportViewModel(repository)

        viewModel.setQuery("OSSFUEL")
        assertEquals(
            listOf("ossfuel"),
            (viewModel.state.value as ImportUiState.Content)
                .filteredRepositories.map { it.name },
        )

        viewModel.setQuery("accounting layer")
        assertEquals(
            2,
            (viewModel.state.value as ImportUiState.Content).filteredRepositories.size,
        )

        viewModel.setQuery("no-such-repository")
        assertTrue(
            (viewModel.state.value as ImportUiState.Content)
                .filteredRepositories.isEmpty(),
        )
    }

    @Test
    fun importMovesToTheNewProject() = runTest {
        val repository = FakeProjectRepository().apply {
            available = listOf(sampleRepository(name = "ossfuel"))
            importedProject = sampleProject(id = "project-9")
        }

        val viewModel = ImportViewModel(repository)
        viewModel.importProject(sampleRepository(name = "ossfuel"))

        val state = viewModel.state.value as ImportUiState.Content
        assertEquals("project-9", state.importedProjectId)
        assertNull(state.importingRepositoryId)
        assertEquals(listOf("octocat/ossfuel"), repository.importedFullNames)

        viewModel.consumeNavigation()
        assertNull(
            (viewModel.state.value as ImportUiState.Content).importedProjectId,
        )
    }

    @Test
    fun failedImportKeepsTheListAndShowsTheError() = runTest {
        val repository = FakeProjectRepository().apply {
            available = listOf(sampleRepository(name = "ossfuel"))
            importError = ApiException(409, "This project is already registered.")
        }

        val viewModel = ImportViewModel(repository)
        viewModel.importProject(sampleRepository(name = "ossfuel"))

        val state = viewModel.state.value as ImportUiState.Content
        assertEquals("This project is already registered.", state.error)
        assertNull(state.importingRepositoryId)
        assertNull(state.importedProjectId)
    }

    @Test
    fun alreadyImportedRepositoriesAreNotImportedAgain() = runTest {
        val repository = FakeProjectRepository()
        val imported = sampleRepository(id = 1L, alreadyImported = true)

        val viewModel = ImportViewModel(repository)
        viewModel.importProject(imported)

        assertTrue(repository.importedFullNames.isEmpty())
        assertNull(
            (viewModel.state.value as ImportUiState.Content).importedProjectId,
        )
    }
}
