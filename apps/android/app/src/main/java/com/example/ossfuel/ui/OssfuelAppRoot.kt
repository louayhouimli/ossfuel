package com.example.ossfuel.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.example.ossfuel.R
import com.example.ossfuel.data.AuthUser

@Composable
fun OssfuelAppRoot(
    state: AuthUiState,
    onSignIn: () -> Unit,
    onSignOut: () -> Unit,
) {
    Surface(
        modifier = Modifier.fillMaxSize(),
        color = MaterialTheme.colorScheme.background,
    ) {
        when (state) {
            AuthUiState.Loading -> LoadingScreen()
            is AuthUiState.SignedOut -> SignInScreen(state.message, onSignIn)
            is AuthUiState.SignedIn -> SignedInScreen(state.user, onSignOut)
        }
    }
}

@Composable
private fun LoadingScreen() {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .safeDrawingPadding()
            .padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        CircularProgressIndicator()
        Spacer(Modifier.height(20.dp))
        Text(
            text = stringResource(R.string.sign_in_checking),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun SignInScreen(message: String?, onSignIn: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .safeDrawingPadding()
            .padding(horizontal = 28.dp, vertical = 32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Text(
            text = stringResource(R.string.sign_in_title),
            style = MaterialTheme.typography.displaySmall,
            fontWeight = FontWeight.Bold,
        )

        Spacer(Modifier.height(14.dp))

        Text(
            text = stringResource(R.string.sign_in_subtitle),
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
        )

        if (message != null) {
            Spacer(Modifier.height(20.dp))
            Text(
                text = message,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.error,
                textAlign = TextAlign.Center,
            )
        }

        Spacer(Modifier.height(36.dp))

        Button(
            onClick = onSignIn,
            modifier = Modifier
                .fillMaxWidth()
                .height(52.dp),
        ) {
            Text(
                text = stringResource(R.string.sign_in_button),
                style = MaterialTheme.typography.titleMedium,
            )
        }

        Spacer(Modifier.height(16.dp))

        Text(
            text = stringResource(R.string.sign_in_hint),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
        )
    }
}

@Composable
private fun SignedInScreen(user: AuthUser, onSignOut: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .safeDrawingPadding()
            .padding(horizontal = 28.dp, vertical = 32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Avatar(user)

        Spacer(Modifier.height(20.dp))

        Text(
            text = user.name ?: user.login,
            style = MaterialTheme.typography.headlineSmall,
            fontWeight = FontWeight.SemiBold,
            textAlign = TextAlign.Center,
        )

        Spacer(Modifier.height(4.dp))

        Text(
            text = "@${user.login}",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        Spacer(Modifier.height(12.dp))

        Text(
            text = stringResource(R.string.sign_in_github),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        Spacer(Modifier.height(32.dp))

        OutlinedButton(onClick = onSignOut) {
            Text(stringResource(R.string.sign_out_button))
        }
    }
}

@Composable
private fun Avatar(user: AuthUser) {
    val initial = (user.name?.firstOrNull() ?: user.login.firstOrNull())
        ?.uppercase()
        ?: "?"

    Box(
        modifier = Modifier
            .size(72.dp)
            .clip(CircleShape)
            .background(MaterialTheme.colorScheme.primaryContainer),
        contentAlignment = Alignment.Center,
    ) {
        Text(
            text = initial,
            style = MaterialTheme.typography.headlineMedium,
            color = MaterialTheme.colorScheme.onPrimaryContainer,
        )
    }
}
