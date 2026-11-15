package com.example.ossfuel.ui.projects

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.example.ossfuel.data.AiUsage
import com.example.ossfuel.data.ApiException
import com.example.ossfuel.data.Balance
import com.example.ossfuel.data.BalanceTransaction
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

sealed interface BalanceUiState {
    data object Loading : BalanceUiState

    data class Content(val balance: Balance) : BalanceUiState

    data class Error(val message: String) : BalanceUiState
}

sealed interface FundingHistoryUiState {
    data object Loading : FundingHistoryUiState

    data class Content(
        val transactions: List<BalanceTransaction>,
        val page: Int,
        val totalPages: Int,
        val isPaging: Boolean = false,
        val error: String? = null,
    ) : FundingHistoryUiState

    data class Error(val message: String) : FundingHistoryUiState
}

sealed interface AiUsageUiState {
    data object Loading : AiUsageUiState

    data class Content(val usages: List<AiUsage>) : AiUsageUiState

    data class Error(val message: String) : AiUsageUiState
}

class ProjectDetailViewModel(
    private val repository: ProjectRepository,
    private val projectId: String,
) : ViewModel() {
    private val _state = MutableStateFlow<ProjectDetailUiState>(ProjectDetailUiState.Loading)
    val state: StateFlow<ProjectDetailUiState> = _state.asStateFlow()

    private val _balance = MutableStateFlow<BalanceUiState>(BalanceUiState.Loading)
    val balance: StateFlow<BalanceUiState> = _balance.asStateFlow()

    private val _history = MutableStateFlow<FundingHistoryUiState>(FundingHistoryUiState.Loading)
    val history: StateFlow<FundingHistoryUiState> = _history.asStateFlow()

    private val _aiUsage = MutableStateFlow<AiUsageUiState>(AiUsageUiState.Loading)
    val aiUsage: StateFlow<AiUsageUiState> = _aiUsage.asStateFlow()

    private var historyPage = 1

    init {
        load()
    }

    fun retry() {
        load()
    }

    fun retryBalance() {
        loadBalance()
    }

    fun retryHistory() {
        loadHistory(historyPage, showLoading = _history.value !is FundingHistoryUiState.Content)
    }

    fun retryAiUsage() {
        loadAiUsage()
    }

    fun previousHistoryPage() {
        val current = _history.value as? FundingHistoryUiState.Content ?: return
        if (current.isPaging || current.page <= 1) {
            return
        }

        loadHistory(current.page - 1, showLoading = false)
    }

    fun nextHistoryPage() {
        val current = _history.value as? FundingHistoryUiState.Content ?: return
        if (current.isPaging || current.page >= current.totalPages) {
            return
        }

        loadHistory(current.page + 1, showLoading = false)
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
                val project = repository.get(projectId)
                loadInsights()
                ProjectDetailUiState.Content(project)
            } catch (exception: ApiException) {
                if (exception.statusCode == 404) {
                    ProjectDetailUiState.NotFound
                } else {
                    ProjectDetailUiState.Error(exception.message ?: DEFAULT_ERROR)
                }
            }
        }
    }

    /**
     * Balance and usage are public, project-scoped reads. They stay separate
     * from the project state so a slow or failing ledger never hides the
     * repository record.
     */
    private fun loadInsights() {
        loadBalance()
        loadHistory(page = 1, showLoading = true)
        loadAiUsage()
    }

    private fun loadBalance() {
        _balance.value = BalanceUiState.Loading

        viewModelScope.launch {
            _balance.value = try {
                BalanceUiState.Content(repository.balance(projectId))
            } catch (exception: ApiException) {
                BalanceUiState.Error(exception.message ?: INSIGHTS_ERROR)
            }
        }
    }

    private fun loadHistory(page: Int, showLoading: Boolean) {
        historyPage = page

        if (showLoading) {
            _history.value = FundingHistoryUiState.Loading
        } else {
            val current = _history.value as? FundingHistoryUiState.Content
            if (current != null) {
                _history.value = current.copy(isPaging = true, error = null)
            }
        }

        viewModelScope.launch {
            try {
                val transactions = repository.balanceTransactions(
                    projectId = projectId,
                    page = page,
                    pageSize = HISTORY_PAGE_SIZE,
                )

                _history.value = FundingHistoryUiState.Content(
                    transactions = transactions.items,
                    page = transactions.page,
                    totalPages = transactions.totalPages,
                )
            } catch (exception: ApiException) {
                val message = exception.message ?: INSIGHTS_ERROR
                val current = _history.value as? FundingHistoryUiState.Content

                _history.value = if (current != null) {
                    current.copy(isPaging = false, error = message)
                } else {
                    FundingHistoryUiState.Error(message)
                }
            }
        }
    }

    private fun loadAiUsage() {
        _aiUsage.value = AiUsageUiState.Loading

        viewModelScope.launch {
            _aiUsage.value = try {
                AiUsageUiState.Content(repository.aiUsage(projectId))
            } catch (exception: ApiException) {
                AiUsageUiState.Error(exception.message ?: INSIGHTS_ERROR)
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
        const val INSIGHTS_ERROR = "Something went wrong. Try again."
        const val HISTORY_PAGE_SIZE = 10
    }
}
