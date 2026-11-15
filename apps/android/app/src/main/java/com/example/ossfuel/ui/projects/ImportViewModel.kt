package com.example.ossfuel.ui.projects

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.example.ossfuel.data.ApiException
import com.example.ossfuel.data.AvailableRepository
import com.example.ossfuel.data.ProjectRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed interface ImportUiState {
    data object Loading : ImportUiState

    data class Error(val message: String) : ImportUiState

    data class Content(
        val repositories: List<AvailableRepository>,
        val query: String = "",
        val importingRepositoryId: Long? = null,
        val error: String? = null,
        val importedProjectId: String? = null,
    ) : ImportUiState {
        val filteredRepositories: List<AvailableRepository>
            get() {
                val term = query.trim()
                if (term.isEmpty()) {
                    return repositories
                }

                return repositories.filter {
                    it.fullName.contains(term, ignoreCase = true) ||
                        it.description?.contains(term, ignoreCase = true) == true
                }
            }
    }
}

class ImportViewModel(private val repository: ProjectRepository) : ViewModel() {
    private val _state = MutableStateFlow<ImportUiState>(ImportUiState.Loading)
    val state: StateFlow<ImportUiState> = _state.asStateFlow()

    init {
        load()
    }

    fun retry() {
        load()
    }

    fun setQuery(query: String) {
        val current = _state.value as? ImportUiState.Content ?: return
        _state.value = current.copy(query = query)
    }

    fun importProject(repositoryToImport: AvailableRepository) {
        val current = _state.value as? ImportUiState.Content ?: return
        if (repositoryToImport.alreadyImported || current.importingRepositoryId != null) {
            return
        }

        _state.value = current.copy(
            importingRepositoryId = repositoryToImport.id,
            error = null,
        )

        viewModelScope.launch {
            try {
                val project = repository.importProject(repositoryToImport.fullName)
                val latest = _state.value as? ImportUiState.Content ?: current
                _state.value = latest.copy(
                    importingRepositoryId = null,
                    importedProjectId = project.id,
                )
            } catch (exception: ApiException) {
                val latest = _state.value as? ImportUiState.Content ?: current
                _state.value = latest.copy(
                    importingRepositoryId = null,
                    error = exception.message ?: DEFAULT_ERROR,
                )
            }
        }
    }

    fun consumeNavigation() {
        val current = _state.value as? ImportUiState.Content ?: return
        _state.value = current.copy(importedProjectId = null)
    }

    private fun load() {
        _state.value = ImportUiState.Loading

        viewModelScope.launch {
            _state.value = try {
                ImportUiState.Content(repository.availableRepositories())
            } catch (exception: ApiException) {
                ImportUiState.Error(exception.message ?: DEFAULT_ERROR)
            }
        }
    }

    class Factory(private val repository: ProjectRepository) : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            require(modelClass.isAssignableFrom(ImportViewModel::class.java)) {
                "Unknown ViewModel class: ${modelClass.name}"
            }

            return ImportViewModel(repository) as T
        }
    }

    private companion object {
        const val DEFAULT_ERROR = "Your repositories could not be loaded. Try again shortly."
    }
}
