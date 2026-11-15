package com.example.ossfuel.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.ossfuel.R
import com.example.ossfuel.ui.components.ErrorNotice
import com.example.ossfuel.ui.components.GitHubSignInButton
import com.example.ossfuel.ui.components.OssfuelLogo

/**
 * Mirrors the web app's `AuthShell`: a dark brand panel beside a light form on
 * wide screens, and the form alone on phones. Signing in is the only way past
 * this screen, so there is no dismiss action.
 */
@Composable
fun SignInScreen(
    message: String?,
    onSignIn: () -> Unit,
) {
    BoxWithConstraints(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .safeDrawingPadding(),
    ) {
        val wide = maxWidth >= 840.dp

        if (wide) {
            Row(modifier = Modifier.fillMaxSize()) {
                BrandPanel(
                    modifier = Modifier
                        .weight(0.9f)
                        .fillMaxHeight(),
                )
                FormColumn(
                    message = message,
                    onSignIn = onSignIn,
                    showLogo = false,
                    modifier = Modifier
                        .weight(1.1f)
                        .fillMaxHeight(),
                )
            }
        } else {
            FormColumn(
                message = message,
                onSignIn = onSignIn,
                showLogo = true,
                modifier = Modifier.fillMaxSize(),
            )
        }
    }
}

@Composable
private fun BrandPanel(modifier: Modifier = Modifier) {
    val foreground = MaterialTheme.colorScheme.onPrimary

    Column(
        modifier = modifier
            .background(MaterialTheme.colorScheme.primary)
            .padding(horizontal = 48.dp, vertical = 48.dp),
        verticalArrangement = Arrangement.SpaceBetween,
    ) {
        OssfuelLogo(
            iconSize = 34.dp,
            color = foreground,
        )

        Column {
            Text(
                text = stringResource(R.string.auth_panel_headline),
                style = MaterialTheme.typography.displaySmall,
                fontWeight = FontWeight.Bold,
                color = foreground,
            )
            Text(
                text = stringResource(R.string.auth_panel_description),
                modifier = Modifier.padding(top = 24.dp),
                style = MaterialTheme.typography.bodyLarge,
                color = foreground.copy(alpha = 0.65f),
            )
        }

        Text(
            text = stringResource(R.string.auth_panel_footer),
            style = MaterialTheme.typography.bodySmall,
            color = foreground.copy(alpha = 0.45f),
        )
    }
}

@Composable
private fun FormColumn(
    message: String?,
    onSignIn: () -> Unit,
    showLogo: Boolean,
    modifier: Modifier = Modifier,
) {
    Column(
        modifier = modifier.padding(horizontal = 24.dp, vertical = 24.dp),
    ) {
        if (showLogo) {
            OssfuelLogo(
                iconSize = 30.dp,
                modifier = Modifier.padding(top = 4.dp),
            )
        }

        Box(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth(),
            contentAlignment = Alignment.Center,
        ) {
            SignInForm(
                message = message,
                onSignIn = onSignIn,
                modifier = Modifier
                    .widthIn(max = 420.dp)
                    .fillMaxWidth(),
            )
        }
    }
}

@Composable
private fun SignInForm(
    message: String?,
    onSignIn: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Column(modifier = modifier) {
        Text(
            text = stringResource(R.string.sign_in_title),
            style = MaterialTheme.typography.displaySmall,
            fontWeight = FontWeight.Bold,
        )

        Text(
            text = stringResource(R.string.sign_in_subtitle),
            modifier = Modifier.padding(top = 18.dp),
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        HorizontalDivider(
            modifier = Modifier.padding(top = 32.dp),
            color = MaterialTheme.colorScheme.outlineVariant,
        )

        GitHubSignInButton(
            text = stringResource(R.string.sign_in_button),
            onClick = onSignIn,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 24.dp),
        )

        if (message != null) {
            ErrorNotice(
                message = message,
                modifier = Modifier.padding(top = 16.dp),
            )
        }

        Spacer(Modifier.height(16.dp))

        Text(
            text = stringResource(R.string.sign_in_hint),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}
