package com.example.ossfuel.ui

import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import com.example.ossfuel.R
import com.example.ossfuel.data.ProjectRepository
import com.example.ossfuel.ui.components.FullScreenLoader

/**
 * The whole app is gated on GitHub sign-in: no projects are browsable until a
 * session exists. [AuthUiState.SignedIn] hands the session to the project
 * navigation graph.
 */
@Composable
fun OssfuelAppRoot(
    state: AuthUiState,
    projectRepository: ProjectRepository,
    onSignIn: () -> Unit,
    onSignOut: () -> Unit,
) {
    Surface(
        modifier = Modifier.fillMaxSize(),
        color = MaterialTheme.colorScheme.background,
    ) {
        when (state) {
            AuthUiState.Loading -> FullScreenLoader(
                label = stringResource(R.string.sign_in_checking),
            )

            is AuthUiState.SignedOut -> SignInScreen(
                message = state.message,
                onSignIn = onSignIn,
            )

            is AuthUiState.SignedIn -> OssfuelNavHost(
                user = state.user,
                repository = projectRepository,
                onSignOut = onSignOut,
            )
        }
    }
}
