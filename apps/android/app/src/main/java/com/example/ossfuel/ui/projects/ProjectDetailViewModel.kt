package com.example.ossfuel.ui.projects

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.example.ossfuel.data.AiUsage
import com.example.ossfuel.data.ApiException
import com.example.ossfuel.data.ApiKeySummary
import com.example.ossfuel.data.Balance
import com.example.ossfuel.data.BalanceTransaction
import com.example.ossfuel.data.CreateApiKeyResponse
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

/**
 * The maintainer-only API key manager. Keys, the creation form, and the
 * one-time raw key share one state so a failed refresh never discards the
 * name being typed or the key that still needs to be copied.
 */
data class ApiKeysUiState(
    val keys: List<ApiKeySummary> = emptyList(),
    val isLoading: Boolean = true,
    val loadError: String? = null,
    val draftName: String = "",
    val isCreating: Boolean = false,
    val createError: String? = null,
    val createdKey: CreateApiKeyResponse? = null,
    val revokingKeyId: String? = null,
    val revokeError: String? = null,
)

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

    private val _apiKeys = MutableStateFlow(ApiKeysUiState())
    val apiKeys: StateFlow<ApiKeysUiState> = _apiKeys.asStateFlow()

    private var historyPage = 1
    private var apiKeysRequested = false

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

    /**
     * The key manager only renders for the maintainer, so it asks for the list
     * when the section first appears instead of loading it for every viewer
     * (the endpoint is owner-only and would answer 403).
     */
    fun loadApiKeys() {
        if (apiKeysRequested) {
            return
        }

        apiKeysRequested = true
        fetchApiKeys()
    }

    fun retryApiKeys() {
        apiKeysRequested = true
        fetchApiKeys()
    }

    fun setApiKeyName(name: String) {
        _apiKeys.value = _apiKeys.value.copy(
            draftName = name.take(MAX_API_KEY_NAME_LENGTH),
            createError = null,
        )
    }

    fun createApiKey() {
        val current = _apiKeys.value
        val name = current.draftName.trim()

        if (name.isEmpty() || current.isCreating) {
            return
        }

        _apiKeys.value = current.copy(isCreating = true, createError = null)

        viewModelScope.launch {
            try {
                val created = repository.createApiKey(projectId, name)
                val latest = _apiKeys.value

                _apiKeys.value = latest.copy(
                    keys = listOf(created.toSummary()) + latest.keys,
                    isLoading = false,
                    isCreating = false,
                    createdKey = created,
                    draftName = "",
                )
            } catch (exception: ApiException) {
                _apiKeys.value = _apiKeys.value.copy(
                    isCreating = false,
                    createError = exception.message ?: API_KEYS_ERROR,
                )
            }
        }
    }

    fun dismissCreatedApiKey() {
        _apiKeys.value = _apiKeys.value.copy(createdKey = null)
    }

    fun revokeApiKey(keyId: String) {
        val current = _apiKeys.value

        if (current.revokingKeyId != null) {
            return
        }

        _apiKeys.value = current.copy(revokingKeyId = keyId, revokeError = null)

        viewModelScope.launch {
            try {
                repository.revokeApiKey(projectId, keyId)
                val latest = _apiKeys.value

                _apiKeys.value = latest.copy(
                    keys = latest.keys.map { key ->
                        if (key.id == keyId) key.copy(isActive = false) else key
                    },
                    revokingKeyId = null,
                )
            } catch (exception: ApiException) {
                _apiKeys.value = _apiKeys.value.copy(
                    revokingKeyId = null,
                    revokeError = exception.message ?: API_KEYS_ERROR,
                )
            }
        }
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

    private fun fetchApiKeys() {
        _apiKeys.value = _apiKeys.value.copy(isLoading = true, loadError = null)

        viewModelScope.launch {
            try {
                val keys = repository.apiKeys(projectId)

                _apiKeys.value = _apiKeys.value.copy(
                    keys = keys,
                    isLoading = false,
                    loadError = null,
                )
            } catch (exception: ApiException) {
                _apiKeys.value = _apiKeys.value.copy(
                    isLoading = false,
                    loadError = exception.message ?: API_KEYS_ERROR,
                )
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
        const val API_KEYS_ERROR = "Something went wrong. Try again."
        const val HISTORY_PAGE_SIZE = 10
        const val MAX_API_KEY_NAME_LENGTH = 100
    }
}

private fun CreateApiKeyResponse.toSummary() = ApiKeySummary(
    id = id,
    projectId = projectId,
    name = name,
    keyPrefix = keyPrefix,
    isActive = true,
    createdAt = createdAt,
)
