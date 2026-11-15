package com.example.ossfuel.ui.projects

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.example.ossfuel.data.ApiException
import com.example.ossfuel.data.ProjectRepository
import com.example.ossfuel.data.ProjectSummary
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed interface ProjectsUiState {
    data object Loading : ProjectsUiState

    data class Error(val message: String) : ProjectsUiState

    data class Content(
        val projects: List<ProjectSummary>,
        val page: Int,
        val totalPages: Int,
        val isRefreshing: Boolean = false,
        val error: String? = null,
    ) : ProjectsUiState
}

class ProjectsViewModel(private val repository: ProjectRepository) : ViewModel() {
    private val _state = MutableStateFlow<ProjectsUiState>(ProjectsUiState.Loading)
    val state: StateFlow<ProjectsUiState> = _state.asStateFlow()

    private var page = 1
    private var loading = false

    init {
        load(page, initial = true)
    }

    fun retry() {
        load(page, initial = true)
    }

    fun refresh() {
        load(page, initial = _state.value !is ProjectsUiState.Content)
    }

    fun previousPage() {
        if (page > 1) {
            load(page - 1, initial = false)
        }
    }

    fun nextPage() {
        val content = _state.value as? ProjectsUiState.Content ?: return
        if (page < content.totalPages) {
            load(page + 1, initial = false)
        }
    }

    private fun load(targetPage: Int, initial: Boolean) {
        if (loading) {
            return
        }

        loading = true
        val previous = _state.value as? ProjectsUiState.Content

        viewModelScope.launch {
            if (initial) {
                _state.value = ProjectsUiState.Loading
            } else if (previous != null) {
                _state.value = previous.copy(isRefreshing = true, error = null)
            }

            try {
                val result = repository.list(targetPage, PAGE_SIZE)
                page = result.page
                _state.value = ProjectsUiState.Content(
                    projects = result.items,
                    page = result.page,
                    totalPages = result.totalPages,
                )
            } catch (exception: ApiException) {
                val message = exception.message ?: DEFAULT_ERROR
                _state.value = if (initial || previous == null) {
                    ProjectsUiState.Error(message)
                } else {
                    previous.copy(isRefreshing = false, error = message)
                }
            } finally {
                loading = false
            }
        }
    }

    class Factory(private val repository: ProjectRepository) : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            require(modelClass.isAssignableFrom(ProjectsViewModel::class.java)) {
                "Unknown ViewModel class: ${modelClass.name}"
            }

            return ProjectsViewModel(repository) as T
        }
    }

    private companion object {
        const val PAGE_SIZE = 12
        const val DEFAULT_ERROR = "Projects could not be loaded. Try again shortly."
    }
}
