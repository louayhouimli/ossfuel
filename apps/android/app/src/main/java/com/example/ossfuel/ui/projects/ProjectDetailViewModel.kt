package com.example.ossfuel.ui.projects

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.example.ossfuel.data.ApiException
import com.example.ossfuel.data.Project
import com.example.ossfuel.data.ProjectRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed interface ProjectDetailUiState {
    data object Loading : ProjectDetailUiState

    data object NotFound : ProjectDetailUiState

    data class Error(val message: String) : ProjectDetailUiState

    data class Content(
        val project: Project,
        val isRefreshing: Boolean = false,
        val isDeleting: Boolean = false,
        val actionError: String? = null,
        val deleted: Boolean = false,
    ) : ProjectDetailUiState
}

class ProjectDetailViewModel(
    private val repository: ProjectRepository,
    private val projectId: String,
) : ViewModel() {
    private val _state = MutableStateFlow<ProjectDetailUiState>(ProjectDetailUiState.Loading)
    val state: StateFlow<ProjectDetailUiState> = _state.asStateFlow()

    init {
        load()
    }

    fun retry() {
        load()
    }

    fun refresh() {
        val current = _state.value as? ProjectDetailUiState.Content ?: return
        if (current.isRefreshing || current.isDeleting) {
            return
        }

        _state.value = current.copy(isRefreshing = true, actionError = null)

        viewModelScope.launch {
            try {
                val project = repository.refresh(projectId)
                val latest = _state.value as? ProjectDetailUiState.Content ?: current
                _state.value = latest.copy(
                    project = project,
                    isRefreshing = false,
                )
            } catch (exception: ApiException) {
                val latest = _state.value as? ProjectDetailUiState.Content ?: current
                _state.value = latest.copy(
                    isRefreshing = false,
                    actionError = exception.message ?: DEFAULT_ERROR,
                )
            }
        }
    }

    fun delete() {
        val current = _state.value as? ProjectDetailUiState.Content ?: return
        if (current.isDeleting) {
            return
        }

        _state.value = current.copy(isDeleting = true, actionError = null)

        viewModelScope.launch {
            try {
                repository.delete(projectId)
                val latest = _state.value as? ProjectDetailUiState.Content ?: current
                _state.value = latest.copy(isDeleting = false, deleted = true)
            } catch (exception: ApiException) {
                val latest = _state.value as? ProjectDetailUiState.Content ?: current
                _state.value = latest.copy(
                    isDeleting = false,
                    actionError = exception.message ?: DEFAULT_ERROR,
                )
            }
        }
    }

    private fun load() {
        _state.value = ProjectDetailUiState.Loading

        viewModelScope.launch {
            _state.value = try {
                ProjectDetailUiState.Content(repository.get(projectId))
            } catch (exception: ApiException) {
                if (exception.statusCode == 404) {
                    ProjectDetailUiState.NotFound
                } else {
                    ProjectDetailUiState.Error(exception.message ?: DEFAULT_ERROR)
                }
            }
        }
    }

    class Factory(
        private val repository: ProjectRepository,
        private val projectId: String,
    ) : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            require(modelClass.isAssignableFrom(ProjectDetailViewModel::class.java)) {
                "Unknown ViewModel class: ${modelClass.name}"
            }

            return ProjectDetailViewModel(repository, projectId) as T
        }
    }

    private companion object {
        const val DEFAULT_ERROR = "Something went wrong. Try again."
    }
}
