package com.example.ossfuel.ui.projects

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.Code
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material.icons.outlined.Star
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.example.ossfuel.R
import com.example.ossfuel.data.AvailableRepository
import com.example.ossfuel.ui.components.EmptyState
import com.example.ossfuel.ui.components.ErrorNotice
import com.example.ossfuel.ui.components.Eyebrow
import com.example.ossfuel.ui.components.InlineLoader
import com.example.ossfuel.ui.components.OutlineAction
import com.example.ossfuel.ui.components.PageHeader
import com.example.ossfuel.ui.components.PrimaryAction
import com.example.ossfuel.ui.format.formatCount

@Composable
fun ImportScreen(
    state: ImportUiState,
    onRetry: () -> Unit,
    onQueryChange: (String) -> Unit,
    onImport: (AvailableRepository) -> Unit,
    onImported: (String) -> Unit,
) {
    Box(
        modifier = Modifier.fillMaxSize(),
        contentAlignment = Alignment.TopCenter,
    ) {
        Column(
            modifier = Modifier
                .widthIn(max = 960.dp)
                .fillMaxSize()
                .imePadding()
                .padding(horizontal = 20.dp)
                .padding(top = 24.dp),
        ) {
            PageHeader(
                eyebrow = stringResource(R.string.import_eyebrow),
                title = stringResource(R.string.import_title),
                description = stringResource(R.string.import_description),
            )

            Spacer(Modifier.height(24.dp))

            when (state) {
                ImportUiState.Loading -> InlineLoader(
                    label = stringResource(R.string.import_loading),
                )

                is ImportUiState.Error -> Column {
                    ErrorNotice(message = state.message.ifBlank {
                        stringResource(R.string.import_error)
                    })
                    OutlineAction(
                        text = stringResource(R.string.action_retry),
                        onClick = onRetry,
                        modifier = Modifier.padding(top = 16.dp),
                        compact = true,
                    )
                }

                is ImportUiState.Content -> Content(
                    state = state,
                    onQueryChange = onQueryChange,
                    onImport = onImport,
                    onImported = onImported,
                )
            }
        }
    }
}

@Composable
private fun androidx.compose.foundation.layout.ColumnScope.Content(
    state: ImportUiState.Content,
    onQueryChange: (String) -> Unit,
    onImport: (AvailableRepository) -> Unit,
    onImported: (String) -> Unit,
) {
    val importedProjectId = state.importedProjectId

    if (importedProjectId != null) {
        LaunchedEffect(importedProjectId) {
            onImported(importedProjectId)
        }
    }

    SearchField(
        query = state.query,
        onQueryChange = onQueryChange,
    )

    if (state.error != null) {
        ErrorNotice(
            message = state.error,
            modifier = Modifier.padding(top = 16.dp),
        )
    }

    if (state.repositories.isEmpty()) {
        EmptyState(
            title = stringResource(R.string.import_empty_title),
            description = stringResource(R.string.import_empty_description),
            modifier = Modifier.padding(top = 20.dp),
        )
        return
    }

    val repositories = state.filteredRepositories

    if (repositories.isEmpty()) {
        Text(
            text = stringResource(R.string.import_no_matches),
            modifier = Modifier.padding(top = 20.dp),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        return
    }

    LazyColumn(
        modifier = Modifier
            .fillMaxWidth()
            .weight(1f)
            .padding(top = 8.dp),
        contentPadding = PaddingValues(bottom = 24.dp),
    ) {
        items(repositories, key = { it.id }) { repository ->
            RepositoryRow(
                repository = repository,
                importing = state.importingRepositoryId == repository.id,
                busy = state.importingRepositoryId != null,
                onImport = { onImport(repository) },
            )
            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
        }
    }
}

@Composable
private fun SearchField(
    query: String,
    onQueryChange: (String) -> Unit,
) {
    OutlinedTextField(
        value = query,
        onValueChange = onQueryChange,
        modifier = Modifier.fillMaxWidth(),
        singleLine = true,
        shape = MaterialTheme.shapes.small,
        placeholder = {
            Text(
                text = stringResource(R.string.import_search_placeholder),
                style = MaterialTheme.typography.bodyMedium,
            )
        },
        leadingIcon = {
            Icon(
                imageVector = Icons.Outlined.Search,
                contentDescription = stringResource(R.string.cd_search),
                modifier = Modifier.size(18.dp),
            )
        },
        trailingIcon = {
            if (query.isNotEmpty()) {
                IconButton(onClick = { onQueryChange("") }) {
                    Icon(
                        imageVector = Icons.Outlined.Close,
                        contentDescription = stringResource(R.string.cd_clear_search),
                        modifier = Modifier.size(18.dp),
                    )
                }
            }
        },
        keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
        colors = TextFieldDefaults.colors(
            focusedContainerColor = MaterialTheme.colorScheme.surfaceContainerLow,
            unfocusedContainerColor = MaterialTheme.colorScheme.surfaceContainerLow,
        ),
    )
}

@Composable
private fun RepositoryRow(
    repository: AvailableRepository,
    importing: Boolean,
    busy: Boolean,
    onImport: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 18.dp),
    ) {
        Eyebrow(repository.ownerLogin)

        Text(
            text = repository.name,
            modifier = Modifier.padding(top = 8.dp),
            style = MaterialTheme.typography.titleMedium,
            fontWeight = FontWeight.Bold,
        )

        Text(
            text = repository.description ?: stringResource(R.string.import_no_description),
            modifier = Modifier.padding(top = 8.dp),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            maxLines = 2,
            overflow = TextOverflow.Ellipsis,
        )

        Row(
            modifier = Modifier.padding(top = 10.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(6.dp),
            ) {
                Icon(
                    imageVector = Icons.Outlined.Star,
                    contentDescription = stringResource(R.string.cd_star),
                    modifier = Modifier.size(14.dp),
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Text(
                    text = formatCount(repository.stars),
                    style = MaterialTheme.typography.bodyMedium,
                )
            }

            if (!repository.language.isNullOrBlank()) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(6.dp),
                ) {
                    Icon(
                        imageVector = Icons.Outlined.Code,
                        contentDescription = null,
                        modifier = Modifier.size(14.dp),
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Text(
                        text = repository.language,
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }

        Spacer(Modifier.height(14.dp))

        if (repository.alreadyImported) {
            Text(
                text = stringResource(R.string.import_already_registered),
                style = MaterialTheme.typography.labelLarge,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        } else {
            PrimaryAction(
                text = stringResource(
                    if (importing) R.string.action_registering else R.string.action_register,
                ),
                onClick = onImport,
                enabled = !busy,
            )
        }
    }
}
