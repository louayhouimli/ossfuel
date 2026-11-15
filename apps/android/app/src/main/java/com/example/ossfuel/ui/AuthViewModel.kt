package com.example.ossfuel.ui

import android.net.Uri
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.example.ossfuel.AppLog
import com.example.ossfuel.data.AuthRepository
import com.example.ossfuel.data.AuthUser
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed interface AuthUiState {
    data object Loading : AuthUiState

    data class SignedOut(val message: String? = null) : AuthUiState

    data class SignedIn(val user: AuthUser) : AuthUiState
}

class AuthViewModel(private val repository: AuthRepository) : ViewModel() {
    private val _state = MutableStateFlow<AuthUiState>(AuthUiState.Loading)
    val state: StateFlow<AuthUiState> = _state.asStateFlow()

    private var lastHandledDeepLink: String? = null
    private var sessionRequest = 0

    init {
        val request = ++sessionRequest

        viewModelScope.launch {
            val user = repository.restoreSession()
            AppLog.debug("session restore finished: signedIn=${user != null}")

            if (request == sessionRequest) {
                _state.value = user
                    ?.let(AuthUiState::SignedIn)
                    ?: AuthUiState.SignedOut()
            }
        }
    }

    fun beginSignIn(): String = repository.beginSignIn()

    fun signOut() {
        sessionRequest++
        repository.signOut()
        _state.value = AuthUiState.SignedOut()
    }

    fun handleDeepLink(uri: Uri?) {
        if (uri == null) {
            return
        }

        if (uri.toString() == lastHandledDeepLink) {
            AppLog.debug("ignoring duplicate deep link")
            return
        }

        if (!uri.scheme.equals(AuthRepository.REDIRECT_SCHEME, ignoreCase = true) ||
            !uri.host.equals(AuthRepository.REDIRECT_HOST, ignoreCase = true) ||
            uri.path != AuthRepository.REDIRECT_PATH
        ) {
            AppLog.warn("ignoring non-auth deep link: scheme=${uri.scheme} host=${uri.host} path=${uri.path}")
            return
        }

        lastHandledDeepLink = uri.toString()

        if (!repository.verifyAndClearState(uri.getQueryParameter("state"))) {
            AppLog.warn("deep link state did not match the pending sign-in")
            _state.value = AuthUiState.SignedOut(
                "Sign-in couldn't be verified. Please try again.",
            )
            return
        }

        val code = uri.getQueryParameter("code")

        if (code.isNullOrBlank()) {
            AppLog.warn("deep link is missing the exchange code")
            _state.value = AuthUiState.SignedOut(
                "GitHub didn't return a sign-in code. Please try again.",
            )
            return
        }

        AppLog.debug("exchanging the sign-in code for a token")

        val request = ++sessionRequest

        viewModelScope.launch {
            _state.value = AuthUiState.Loading

            val result = try {
                val user = repository.exchangeCode(code)
                AppLog.debug("signed in as ${user.login}")
                AuthUiState.SignedIn(user)
            } catch (exception: Exception) {
                AppLog.warn("sign-in code exchange failed", exception)
                AuthUiState.SignedOut(
                    exception.message ?: "Sign-in failed. Please try again.",
                )
            }

            if (request == sessionRequest) {
                _state.value = result
            }
        }
    }

    class Factory(private val repository: AuthRepository) : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            require(modelClass.isAssignableFrom(AuthViewModel::class.java)) {
                "Unknown ViewModel class: ${modelClass.name}"
            }

            return AuthViewModel(repository) as T
        }
    }
}
