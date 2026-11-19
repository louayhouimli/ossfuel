package com.example.ossfuel.ui.projects

import android.content.ClipData
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.DeleteOutline
import androidx.compose.material.icons.outlined.Terminal
import androidx.compose.material.icons.outlined.WarningAmber
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalClipboard
import androidx.compose.ui.platform.toClipEntry
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.example.ossfuel.R
import com.example.ossfuel.data.ApiKeySummary
import com.example.ossfuel.data.CreateApiKeyResponse
import com.example.ossfuel.ui.components.CompactLoader
import com.example.ossfuel.ui.components.Eyebrow
import com.example.ossfuel.ui.components.OutlineAction
import com.example.ossfuel.ui.components.PrimaryAction
import com.example.ossfuel.ui.format.formatDate
import com.example.ossfuel.ui.theme.LocalWarningColor
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

private const val COPIED_FEEDBACK_MILLIS = 2_000L

/**
 * Create and revoke the keys that let coding tools spend this project's AI
 * balance through the OpenAI-compatible gateway. Only the maintainer sees
 * this section; the list endpoint is owner-only.
 */
@Composable
fun ApiKeysManager(
    state: ApiKeysUiState,
    gatewayBaseUrl: String,
    onLoad: () -> Unit,
    onNameChange: (String) -> Unit,
    onCreate: () -> Unit,
    onDismissCreatedKey: () -> Unit,
    onRevoke: (String) -> Unit,
    onRetry: () -> Unit,
) {
    LaunchedEffect(Unit) { onLoad() }

    var confirmingRevoke by remember { mutableStateOf<ApiKeySummary?>(null) }

    Column(modifier = Modifier.padding(top = 28.dp)) {
        Eyebrow(text = stringResource(R.string.api_keys_eyebrow))

        Text(
            text = stringResource(R.string.api_keys_title),
            modifier = Modifier.padding(top = 10.dp),
            style = MaterialTheme.typography.titleLarge,
            fontWeight = FontWeight.Bold,
        )

        Text(
            text = stringResource(R.string.api_keys_description),
            modifier = Modifier.padding(top = 8.dp),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        state.createdKey?.let { created ->
            CreatedKeyNotice(
                key = created,
                gatewayBaseUrl = gatewayBaseUrl,
                onDismiss = onDismissCreatedKey,
                modifier = Modifier.padding(top = 16.dp),
            )
        }

        CreateKeyForm(
            name = state.draftName,
            isCreating = state.isCreating,
            onNameChange = onNameChange,
            onCreate = onCreate,
            modifier = Modifier.padding(top = 16.dp),
        )

        if (state.createError != null) {
            Text(
                text = state.createError,
                modifier = Modifier.padding(top = 8.dp),
                style = MaterialTheme.typography.bodySmall,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.error,
            )
        }

        when {
            state.isLoading -> CompactLoader(
                label = stringResource(R.string.api_keys_loading),
                modifier = Modifier.padding(top = 20.dp),
            )

            state.loadError != null -> Column(modifier = Modifier.padding(top = 20.dp)) {
                Text(
                    text = stringResource(R.string.api_keys_error),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                OutlineAction(
                    text = stringResource(R.string.action_retry),
                    onClick = onRetry,
                    modifier = Modifier.padding(top = 12.dp),
                    compact = true,
                )
            }

            state.keys.isEmpty() -> EmptyKeysState(modifier = Modifier.padding(top = 20.dp))

            else -> KeyList(
                keys = state.keys,
                revokingKeyId = state.revokingKeyId,
                onRevokeRequest = { confirmingRevoke = it },
                modifier = Modifier.padding(top = 20.dp),
            )
        }

        if (state.revokeError != null) {
            Text(
                text = state.revokeError,
                modifier = Modifier.padding(top = 12.dp),
                style = MaterialTheme.typography.bodySmall,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.error,
            )
        }
    }

    confirmingRevoke?.let { key ->
        RevokeKeyDialog(
            key = key,
            onConfirm = {
                confirmingRevoke = null
                onRevoke(key.id)
            },
            onDismiss = { confirmingRevoke = null },
        )
    }
}

@Composable
private fun CreatedKeyNotice(
    key: CreateApiKeyResponse,
    gatewayBaseUrl: String,
    onDismiss: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val warningColor = LocalWarningColor.current
    val clipboard = LocalClipboard.current
    val scope = rememberCoroutineScope()

    var copied by remember { mutableStateOf(false) }

    LaunchedEffect(copied) {
        if (copied) {
            delay(COPIED_FEEDBACK_MILLIS)
            copied = false
        }
    }

    Surface(
        modifier = modifier.fillMaxWidth(),
        shape = MaterialTheme.shapes.medium,
        color = warningColor.copy(alpha = 0.10f),
        border = BorderStroke(1.dp, warningColor.copy(alpha = 0.4f)),
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                Icon(
                    imageVector = Icons.Outlined.WarningAmber,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp),
                    tint = warningColor,
                )
                Text(
                    text = stringResource(R.string.api_keys_new_title),
                    style = MaterialTheme.typography.bodyMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = warningColor,
                )
            }

            Surface(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 12.dp),
                shape = MaterialTheme.shapes.extraSmall,
                color = MaterialTheme.colorScheme.surface,
            ) {
                SelectionContainer {
                    Text(
                        text = key.rawKey,
                        modifier = Modifier.padding(horizontal = 12.dp, vertical = 10.dp),
                        style = MaterialTheme.typography.bodySmall,
                        fontFamily = FontFamily.Monospace,
                    )
                }
            }

            Row(
                modifier = Modifier.padding(top = 12.dp),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                PrimaryAction(
                    text = stringResource(
                        if (copied) R.string.api_keys_copied else R.string.api_keys_copy,
                    ),
                    onClick = {
                        scope.launch {
                            clipboard.setClipEntry(
                                ClipData.newPlainText(key.name, key.rawKey).toClipEntry(),
                            )
                        }
                        copied = true
                    },
                )
                OutlineAction(
                    text = stringResource(R.string.api_keys_done),
                    onClick = onDismiss,
                    compact = true,
                )
            }

            Text(
                text = stringResource(R.string.api_keys_usage_hint),
                modifier = Modifier.padding(top = 14.dp),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )

            SelectionContainer {
                Text(
                    text = stringResource(
                        R.string.api_keys_env_hint,
                        gatewayBaseUrl.trimEnd('/'),
                        key.rawKey,
                    ),
                    modifier = Modifier.padding(top = 6.dp),
                    style = MaterialTheme.typography.bodySmall,
                    fontFamily = FontFamily.Monospace,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
    }
}

@Composable
private fun CreateKeyForm(
    name: String,
    isCreating: Boolean,
    onNameChange: (String) -> Unit,
    onCreate: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Column(modifier = modifier) {
        OutlinedTextField(
            value = name,
            onValueChange = onNameChange,
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
            enabled = !isCreating,
            shape = MaterialTheme.shapes.small,
            placeholder = {
                Text(
                    text = stringResource(R.string.api_keys_name_placeholder),
                    style = MaterialTheme.typography.bodyMedium,
                )
            },
            keyboardOptions = KeyboardOptions(imeAction = ImeAction.Done),
            keyboardActions = KeyboardActions(
                onDone = {
                    if (name.isNotBlank()) {
                        onCreate()
                    }
                },
            ),
            colors = TextFieldDefaults.colors(
                focusedContainerColor = MaterialTheme.colorScheme.surfaceContainerLow,
                unfocusedContainerColor = MaterialTheme.colorScheme.surfaceContainerLow,
            ),
        )

        PrimaryAction(
            text = stringResource(
                if (isCreating) R.string.api_keys_creating else R.string.api_keys_create,
            ),
            onClick = onCreate,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 10.dp),
            enabled = !isCreating && name.isNotBlank(),
        )
    }
}

@Composable
private fun KeyList(
    keys: List<ApiKeySummary>,
    revokingKeyId: String?,
    onRevokeRequest: (ApiKeySummary) -> Unit,
    modifier: Modifier = Modifier,
) {
    Surface(
        modifier = modifier.fillMaxWidth(),
        shape = MaterialTheme.shapes.medium,
        color = MaterialTheme.colorScheme.surfaceContainerLow,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
    ) {
        Column {
            keys.forEachIndexed { index, key ->
                ApiKeyRow(
                    key = key,
                    revoking = revokingKeyId == key.id,
                    onRevoke = { onRevokeRequest(key) },
                )

                if (index < keys.lastIndex) {
                    HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                }
            }
        }
    }
}

@Composable
private fun ApiKeyRow(
    key: ApiKeySummary,
    revoking: Boolean,
    onRevoke: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(14.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(
                text = key.name,
                modifier = Modifier.weight(1f),
                style = MaterialTheme.typography.titleSmall,
                fontWeight = FontWeight.SemiBold,
            )

            StatusBadge(active = key.isActive)
        }

        Text(
            text = "${key.keyPrefix}...",
            modifier = Modifier.padding(top = 4.dp),
            style = MaterialTheme.typography.bodySmall,
            fontFamily = FontFamily.Monospace,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        Text(
            text = stringResource(R.string.api_keys_created, formatDate(key.createdAt)),
            modifier = Modifier.padding(top = 6.dp),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        Text(
            text = key.lastUsedAt?.let { lastUsed ->
                stringResource(R.string.api_keys_last_used, formatDate(lastUsed))
            } ?: stringResource(R.string.api_keys_never_used),
            modifier = Modifier.padding(top = 2.dp),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        if (key.isActive) {
            OutlineAction(
                text = stringResource(
                    if (revoking) R.string.api_keys_revoking else R.string.action_revoke,
                ),
                onClick = onRevoke,
                modifier = Modifier.padding(top = 10.dp),
                enabled = !revoking,
                icon = Icons.Outlined.DeleteOutline,
                compact = true,
                contentColor = MaterialTheme.colorScheme.error,
            )
        }
    }
}

@Composable
private fun StatusBadge(active: Boolean) {
    val color = if (active) {
        MaterialTheme.colorScheme.tertiary
    } else {
        MaterialTheme.colorScheme.error
    }

    Surface(
        shape = MaterialTheme.shapes.extraSmall,
        color = color.copy(alpha = 0.12f),
    ) {
        Text(
            text = stringResource(
                if (active) R.string.api_keys_active else R.string.api_keys_revoked,
            ),
            modifier = Modifier.padding(horizontal = 8.dp, vertical = 3.dp),
            style = MaterialTheme.typography.labelSmall,
            color = color,
        )
    }
}

@Composable
private fun EmptyKeysState(modifier: Modifier = Modifier) {
    Surface(
        modifier = modifier.fillMaxWidth(),
        shape = MaterialTheme.shapes.medium,
        color = androidx.compose.ui.graphics.Color.Transparent,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline),
    ) {
        Column(
            modifier = Modifier.padding(24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            Icon(
                imageVector = Icons.Outlined.Terminal,
                contentDescription = null,
                modifier = Modifier.size(24.dp),
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Text(
                text = stringResource(R.string.api_keys_empty),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
            )
        }
    }
}

@Composable
private fun RevokeKeyDialog(
    key: ApiKeySummary,
    onConfirm: () -> Unit,
    onDismiss: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = {
            Text(
                text = stringResource(R.string.api_keys_revoke_title),
                fontWeight = FontWeight.Bold,
            )
        },
        text = {
            Text(text = stringResource(R.string.api_keys_revoke_description, key.name))
        },
        confirmButton = {
            TextButton(
                onClick = onConfirm,
                colors = ButtonDefaults.textButtonColors(
                    contentColor = MaterialTheme.colorScheme.error,
                ),
            ) {
                Text(
                    text = stringResource(R.string.action_revoke),
                    fontWeight = FontWeight.SemiBold,
                )
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text(text = stringResource(R.string.action_cancel))
            }
        },
        containerColor = MaterialTheme.colorScheme.surfaceContainerLow,
        shape = MaterialTheme.shapes.medium,
    )
}
