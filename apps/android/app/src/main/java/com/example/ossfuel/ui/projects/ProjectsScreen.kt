package com.example.ossfuel.ui.projects

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.GridItemSpan
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Code
import androidx.compose.material.icons.outlined.Star
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.example.ossfuel.R
import com.example.ossfuel.data.ProjectSummary
import com.example.ossfuel.ui.components.ClickablePanel
import com.example.ossfuel.ui.components.EmptyState
import com.example.ossfuel.ui.components.ErrorNotice
import com.example.ossfuel.ui.components.Eyebrow
import com.example.ossfuel.ui.components.InlineLoader
import com.example.ossfuel.ui.components.OutlineAction
import com.example.ossfuel.ui.components.PageHeader
import com.example.ossfuel.ui.components.Pagination
import com.example.ossfuel.ui.components.PrimaryAction
import com.example.ossfuel.ui.format.formatCount

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ProjectsScreen(
    state: ProjectsUiState,
    onRetry: () -> Unit,
    onRefresh: () -> Unit,
    onPreviousPage: () -> Unit,
    onNextPage: () -> Unit,
    onProjectClick: (ProjectSummary) -> Unit,
    onImport: () -> Unit,
) {
    val content = state as? ProjectsUiState.Content

    PullToRefreshBox(
        isRefreshing = content?.isRefreshing == true,
        onRefresh = onRefresh,
        modifier = Modifier.fillMaxSize(),
    ) {
        Box(
            modifier = Modifier.fillMaxSize(),
            contentAlignment = Alignment.TopCenter,
        ) {
            LazyVerticalGrid(
                columns = GridCells.Adaptive(minSize = 280.dp),
                modifier = Modifier
                    .fillMaxHeight()
                    .widthIn(max = 1200.dp)
                    .fillMaxWidth(),
                contentPadding = PaddingValues(horizontal = 20.dp, vertical = 24.dp),
                horizontalArrangement = Arrangement.spacedBy(12.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                item(span = { GridItemSpan(maxLineSpan) }) {
                    PageHeader(
                        eyebrow = stringResource(R.string.projects_eyebrow),
                        title = stringResource(R.string.projects_title),
                        description = stringResource(R.string.projects_description),
                        modifier = Modifier.padding(bottom = 12.dp),
                    )
                }

                if (content?.error != null) {
                    item(span = { GridItemSpan(maxLineSpan) }) {
                        ErrorNotice(content.error)
                    }
                }

                when (state) {
                    ProjectsUiState.Loading -> item(span = { GridItemSpan(maxLineSpan) }) {
                        InlineLoader(stringResource(R.string.projects_loading))
                    }

                    is ProjectsUiState.Error -> item(span = { GridItemSpan(maxLineSpan) }) {
                        Column {
                            ErrorNotice(state.message)
                            OutlineAction(
                                text = stringResource(R.string.action_retry),
                                onClick = onRetry,
                                modifier = Modifier.padding(top = 16.dp),
                                compact = true,
                            )
                        }
                    }

                    is ProjectsUiState.Content -> {
                        if (state.projects.isEmpty()) {
                            item(span = { GridItemSpan(maxLineSpan) }) {
                                EmptyState(
                                    title = stringResource(R.string.projects_empty_title),
                                    description = stringResource(R.string.projects_empty_description),
                                    action = {
                                        PrimaryAction(
                                            text = stringResource(R.string.action_register_project),
                                            onClick = onImport,
                                        )
                                    },
                                )
                            }
                        } else {
                            items(state.projects, key = { it.id }) { project ->
                                ProjectCard(
                                    project = project,
                                    onClick = { onProjectClick(project) },
                                )
                            }

                            if (state.totalPages > 1) {
                                item(span = { GridItemSpan(maxLineSpan) }) {
                                    Column {
                                        HorizontalDivider(
                                            modifier = Modifier.padding(vertical = 20.dp),
                                            color = MaterialTheme.colorScheme.outlineVariant,
                                        )
                                        Pagination(
                                            page = state.page,
                                            totalPages = state.totalPages,
                                            onPrevious = onPreviousPage,
                                            onNext = onNextPage,
                                        )
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun ProjectCard(project: ProjectSummary, onClick: () -> Unit) {
    ClickablePanel(
        onClick = onClick,
        modifier = Modifier.fillMaxWidth(),
    ) {
        Eyebrow(project.ownerLogin)

        Text(
            text = project.name,
            modifier = Modifier.padding(top = 8.dp),
            style = MaterialTheme.typography.titleLarge,
            fontWeight = FontWeight.Bold,
            maxLines = 2,
            overflow = TextOverflow.Ellipsis,
        )

        Text(
            text = project.description ?: stringResource(R.string.projects_no_description),
            modifier = Modifier
                .padding(top = 12.dp)
                .heightIn(min = 66.dp),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            maxLines = 3,
            overflow = TextOverflow.Ellipsis,
        )

        HorizontalDivider(
            modifier = Modifier.padding(top = 16.dp),
            color = MaterialTheme.colorScheme.outlineVariant,
        )

        Row(
            modifier = Modifier.padding(top = 14.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(18.dp),
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
                    text = formatCount(project.stars),
                    style = MaterialTheme.typography.labelLarge,
                    fontWeight = FontWeight.SemiBold,
                )
            }

            if (!project.language.isNullOrBlank()) {
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
                        text = project.language,
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }
    }
}
