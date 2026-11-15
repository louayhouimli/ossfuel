package com.example.ossfuel.ui.projects

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.CallSplit
import androidx.compose.material.icons.outlined.AccountTree
import androidx.compose.material.icons.outlined.BugReport
import androidx.compose.material.icons.outlined.CalendarToday
import androidx.compose.material.icons.outlined.Code
import androidx.compose.material.icons.outlined.Folder
import androidx.compose.material.icons.outlined.Public
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material.icons.outlined.Star
import androidx.compose.material.icons.outlined.Tag
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.dp
import com.example.ossfuel.R
import com.example.ossfuel.data.Project
import com.example.ossfuel.ui.components.BackIconButton
import com.example.ossfuel.ui.components.DeleteButton
import com.example.ossfuel.ui.components.ErrorNotice
import com.example.ossfuel.ui.components.Eyebrow
import com.example.ossfuel.ui.components.InlineLoader
import com.example.ossfuel.ui.components.OutlineAction
import com.example.ossfuel.ui.components.PageHeader
import com.example.ossfuel.ui.components.Panel
import com.example.ossfuel.ui.components.PrimaryAction
import com.example.ossfuel.ui.components.RecordRow
import com.example.ossfuel.ui.components.RefreshButton
import com.example.ossfuel.ui.components.TagChip
import com.example.ossfuel.ui.format.formatCount
import com.example.ossfuel.ui.format.formatDate
import com.example.ossfuel.ui.format.formatRelativeTime

@Composable
fun ProjectDetailScreen(
    state: ProjectDetailUiState,
    userId: String,
    balanceState: BalanceUiState,
    historyState: FundingHistoryUiState,
    aiUsageState: AiUsageUiState,
    onBack: () -> Unit,
    onRetry: () -> Unit,
    onRefresh: () -> Unit,
    onDelete: () -> Unit,
    onDeleted: () -> Unit,
    onRetryBalance: () -> Unit,
    onRetryHistory: () -> Unit,
    onPreviousHistoryPage: () -> Unit,
    onNextHistoryPage: () -> Unit,
    onRetryAiUsage: () -> Unit,
) {
    Box(
        modifier = Modifier.fillMaxSize(),
        contentAlignment = Alignment.TopCenter,
    ) {
        Column(
            modifier = Modifier
                .widthIn(max = 960.dp)
                .fillMaxWidth()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 20.dp, vertical = 8.dp),
        ) {
            BackLink(onClick = onBack)

            when (state) {
                ProjectDetailUiState.Loading -> InlineLoader(
                    label = stringResource(R.string.projects_loading),
                    modifier = Modifier.padding(top = 24.dp),
                )

                ProjectDetailUiState.NotFound -> Panel(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 20.dp),
                ) {
                    Text(
                        text = stringResource(R.string.project_not_found_title),
                        style = MaterialTheme.typography.headlineSmall,
                        fontWeight = FontWeight.Bold,
                    )
                    Text(
                        text = stringResource(R.string.project_not_found_description),
                        modifier = Modifier.padding(top = 12.dp),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }

                is ProjectDetailUiState.Error -> Column(
                    modifier = Modifier.padding(top = 20.dp),
                ) {
                    ErrorNotice(
                        message = state.message.ifBlank {
                            stringResource(R.string.project_load_error)
                        },
                    )
                    OutlineAction(
                        text = stringResource(R.string.action_retry),
                        onClick = onRetry,
                        modifier = Modifier.padding(top = 16.dp),
                        compact = true,
                    )
                }

                is ProjectDetailUiState.Content -> {
                    if (state.deleted) {
                        LaunchedEffect(Unit) { onDeleted() }
                    }

                    ProjectRecord(
                        project = state.project,
                        canManage = state.project.ownerUserId.equals(userId, ignoreCase = true),
                        isRefreshing = state.isRefreshing,
                        isDeleting = state.isDeleting,
                        actionError = state.actionError,
                        balanceState = balanceState,
                        historyState = historyState,
                        aiUsageState = aiUsageState,
                        onRefresh = onRefresh,
                        onDelete = onDelete,
                        onRetryBalance = onRetryBalance,
                        onRetryHistory = onRetryHistory,
                        onPreviousHistoryPage = onPreviousHistoryPage,
                        onNextHistoryPage = onNextHistoryPage,
                        onRetryAiUsage = onRetryAiUsage,
                    )
                }
            }

            Spacer(Modifier.height(40.dp))
        }
    }
}

@Composable
private fun BackLink(onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .heightIn(min = 44.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        BackIconButton(onClick = onClick)
        Text(
            text = stringResource(R.string.project_back),
            modifier = Modifier.clickable(onClick = onClick),
            style = MaterialTheme.typography.labelLarge,
        )
    }
}

@Composable
private fun ProjectRecord(
    project: Project,
    canManage: Boolean,
    isRefreshing: Boolean,
    isDeleting: Boolean,
    actionError: String?,
    balanceState: BalanceUiState,
    historyState: FundingHistoryUiState,
    aiUsageState: AiUsageUiState,
    onRefresh: () -> Unit,
    onDelete: () -> Unit,
    onRetryBalance: () -> Unit,
    onRetryHistory: () -> Unit,
    onPreviousHistoryPage: () -> Unit,
    onNextHistoryPage: () -> Unit,
    onRetryAiUsage: () -> Unit,
) {
    val uriHandler = LocalUriHandler.current

    Column(modifier = Modifier.padding(top = 20.dp)) {
        PageHeader(
            eyebrow = project.ownerLogin,
            title = project.name,
            description = project.description ?: stringResource(R.string.projects_no_description),
        )

        OutlineAction(
            text = stringResource(R.string.action_view_on_github),
            onClick = { uriHandler.openUri(project.htmlUrl) },
            modifier = Modifier.padding(top = 20.dp),
            compact = true,
        )

        BalanceSection(
            balanceState = balanceState,
            historyState = historyState,
            onRetryBalance = onRetryBalance,
            onRetryHistory = onRetryHistory,
            onPreviousPage = onPreviousHistoryPage,
            onNextPage = onNextHistoryPage,
        )

        AiUsageAuditSection(
            state = aiUsageState,
            onRetry = onRetryAiUsage,
        )

        Eyebrow(
            text = stringResource(R.string.project_record_heading),
            modifier = Modifier.padding(top = 40.dp),
        )

        HorizontalDivider(
            modifier = Modifier.padding(top = 14.dp),
            color = MaterialTheme.colorScheme.outlineVariant,
        )

        RecordValueRow(
            label = stringResource(R.string.project_record_full_name),
            icon = Icons.Outlined.Folder,
        ) {
            Text(
                text = project.fullName,
                modifier = Modifier.clickable { uriHandler.openUri(project.htmlUrl) },
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.SemiBold,
                textDecoration = TextDecoration.Underline,
            )
        }

        RecordValueRow(
            label = stringResource(R.string.project_record_language),
            icon = Icons.Outlined.Code,
        ) {
            RecordValue(project.language ?: stringResource(R.string.value_not_specified))
        }

        RecordValueRow(
            label = stringResource(R.string.project_record_stars),
            icon = Icons.Outlined.Star,
        ) {
            RecordValue(formatCount(project.stars))
        }

        RecordValueRow(
            label = stringResource(R.string.project_record_forks),
            icon = Icons.AutoMirrored.Outlined.CallSplit,
        ) {
            RecordValue(formatCount(project.forks))
        }

        RecordValueRow(
            label = stringResource(R.string.project_record_open_issues),
            icon = Icons.Outlined.BugReport,
        ) {
            RecordValue(formatCount(project.openIssues))
        }

        RecordValueRow(
            label = stringResource(R.string.project_record_default_branch),
            icon = Icons.Outlined.AccountTree,
        ) {
            RecordValue(project.defaultBranch ?: stringResource(R.string.value_unknown))
        }

        if (!project.homepageUrl.isNullOrBlank()) {
            RecordValueRow(
                label = stringResource(R.string.project_record_website),
                icon = Icons.Outlined.Public,
            ) {
                Text(
                    text = project.homepageUrl,
                    modifier = Modifier.clickable {
                        uriHandler.openUri(project.homepageUrl)
                    },
                    style = MaterialTheme.typography.bodyMedium,
                    fontWeight = FontWeight.SemiBold,
                    textDecoration = TextDecoration.Underline,
                )
            }
        }

        RecordValueRow(
            label = stringResource(R.string.project_record_registered),
            icon = Icons.Outlined.CalendarToday,
        ) {
            RecordValue(formatDate(project.createdAt))
        }

        RecordValueRow(
            label = stringResource(R.string.project_record_last_snapshot),
            icon = Icons.Outlined.Schedule,
        ) {
            RecordValue(formatRelativeTime(project.lastSyncedAt))
        }

        if (project.topics.isNotEmpty()) {
            RecordValueRow(
                label = stringResource(R.string.project_record_topics),
                icon = Icons.Outlined.Tag,
            ) {
                TopicChips(project.topics)
            }
        }

        if (canManage) {
            MaintainerSection(
                project = project,
                isRefreshing = isRefreshing,
                isDeleting = isDeleting,
                actionError = actionError,
                onRefresh = onRefresh,
                onDelete = onDelete,
            )
        }
    }
}

@Composable
private fun RecordValueRow(
    label: String,
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    content: @Composable () -> Unit,
) {
    RecordRow(label = label, icon = icon) {
        content()
    }
}

@Composable
private fun RecordValue(text: String) {
    Text(
        text = text,
        style = MaterialTheme.typography.bodyMedium,
        fontWeight = FontWeight.SemiBold,
    )
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun TopicChips(topics: List<String>) {
    FlowRow(
        horizontalArrangement = Arrangement.spacedBy(8.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        topics.forEach { topic ->
            TagChip(text = topic)
        }
    }
}

@Composable
private fun MaintainerSection(
    project: Project,
    isRefreshing: Boolean,
    isDeleting: Boolean,
    actionError: String?,
    onRefresh: () -> Unit,
    onDelete: () -> Unit,
) {
    var confirmingDelete by remember { mutableStateOf(false) }

    Column(modifier = Modifier.padding(top = 40.dp)) {
        HorizontalDivider(
            color = MaterialTheme.colorScheme.outlineVariant,
        )

        Eyebrow(
            text = stringResource(R.string.project_maintainer_heading),
            modifier = Modifier.padding(top = 28.dp),
        )

        Text(
            text = stringResource(R.string.project_maintainer_description),
            modifier = Modifier.padding(top = 10.dp),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        FlowRow(
            modifier = Modifier.padding(top = 20.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            RefreshButton(
                onClick = onRefresh,
                enabled = !isRefreshing && !isDeleting,
                refreshing = isRefreshing,
            )
            DeleteButton(
                onClick = {
                    confirmingDelete = true
                },
                enabled = !isRefreshing && !isDeleting,
            )
        }

        if (actionError != null && !confirmingDelete) {
            ErrorNotice(
                message = actionError,
                modifier = Modifier.padding(top = 16.dp),
            )
        }

        if (confirmingDelete) {
            DeleteConfirmation(
                project = project,
                isDeleting = isDeleting,
                actionError = actionError,
                onConfirm = onDelete,
                onCancel = {
                    confirmingDelete = false
                },
            )
        }
    }
}

@Composable
private fun DeleteConfirmation(
    project: Project,
    isDeleting: Boolean,
    actionError: String?,
    onConfirm: () -> Unit,
    onCancel: () -> Unit,
) {
    Panel(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 20.dp),
    ) {
        Text(
            text = stringResource(R.string.project_delete_title),
            style = MaterialTheme.typography.headlineSmall,
            fontWeight = FontWeight.Bold,
        )

        Text(
            text = stringResource(R.string.project_delete_description, project.fullName),
            modifier = Modifier.padding(top = 10.dp),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        Row(
            modifier = Modifier.padding(top = 20.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            PrimaryAction(
                text = stringResource(
                    if (isDeleting) R.string.action_deleting else R.string.action_confirm_delete,
                ),
                onClick = onConfirm,
                enabled = !isDeleting,
            )
            OutlineAction(
                text = stringResource(R.string.action_keep_it),
                onClick = onCancel,
                enabled = !isDeleting,
                compact = true,
            )
        }

        if (actionError != null) {
            ErrorNotice(
                message = actionError,
                modifier = Modifier.padding(top = 16.dp),
            )
        }
    }
}
