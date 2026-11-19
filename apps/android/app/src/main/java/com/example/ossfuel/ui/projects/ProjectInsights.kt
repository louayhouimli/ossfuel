package com.example.ossfuel.ui.projects

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Bolt
import androidx.compose.material.icons.outlined.Memory
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.example.ossfuel.R
import com.example.ossfuel.data.AiUsage
import com.example.ossfuel.data.BalanceTransaction
import com.example.ossfuel.ui.components.CompactLoader
import com.example.ossfuel.ui.components.Eyebrow
import com.example.ossfuel.ui.components.OutlineAction
import com.example.ossfuel.ui.components.Pagination
import com.example.ossfuel.ui.components.Panel
import com.example.ossfuel.ui.format.formatCount
import com.example.ossfuel.ui.format.formatDateTime
import com.example.ossfuel.ui.format.formatMoney
import com.example.ossfuel.ui.format.formatShortDateTime

/**
 * The project's AI Balance and every credit that built it. Both stay public,
 * mirroring the web client: a sponsor can check where a payment went, and the
 * sponsor's own funding list remains private elsewhere.
 */
@Composable
fun BalanceSection(
    balanceState: BalanceUiState,
    historyState: FundingHistoryUiState,
    onRetryBalance: () -> Unit,
    onRetryHistory: () -> Unit,
    onPreviousPage: () -> Unit,
    onNextPage: () -> Unit,
) {
    Panel(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 32.dp),
    ) {
        Eyebrow(text = stringResource(R.string.balance_eyebrow))

        when (balanceState) {
            BalanceUiState.Loading -> CompactLoader(
                label = stringResource(R.string.balance_loading),
                modifier = Modifier.padding(top = 16.dp),
            )

            is BalanceUiState.Error -> SectionError(
                message = stringResource(R.string.balance_error),
                onRetry = onRetryBalance,
                modifier = Modifier.padding(top = 16.dp),
            )

            is BalanceUiState.Content -> {
                Text(
                    text = formatMoney(
                        amountMinor = balanceState.balance.amountMinor,
                        currency = balanceState.balance.currency,
                    ),
                    modifier = Modifier.padding(top = 12.dp),
                    style = MaterialTheme.typography.displaySmall,
                    fontWeight = FontWeight.Bold,
                )
                Text(
                    text = stringResource(
                        R.string.balance_caption,
                        balanceState.balance.currency.uppercase(),
                    ),
                    modifier = Modifier.padding(top = 8.dp),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }

        HorizontalDivider(
            modifier = Modifier.padding(top = 24.dp),
            color = MaterialTheme.colorScheme.outlineVariant,
        )

        Eyebrow(
            text = stringResource(R.string.balance_history_heading),
            modifier = Modifier.padding(top = 24.dp),
        )

        FundingHistory(
            state = historyState,
            onRetry = onRetryHistory,
            onPreviousPage = onPreviousPage,
            onNextPage = onNextPage,
        )
    }
}

@Composable
private fun FundingHistory(
    state: FundingHistoryUiState,
    onRetry: () -> Unit,
    onPreviousPage: () -> Unit,
    onNextPage: () -> Unit,
) {
    when (state) {
        FundingHistoryUiState.Loading -> CompactLoader(
            label = stringResource(R.string.balance_history_loading),
            modifier = Modifier.padding(top = 16.dp),
        )

        is FundingHistoryUiState.Error -> SectionError(
            message = stringResource(R.string.balance_history_error),
            onRetry = onRetry,
            modifier = Modifier.padding(top = 16.dp),
        )

        is FundingHistoryUiState.Content -> {
            if (state.transactions.isEmpty()) {
                Text(
                    text = stringResource(R.string.balance_history_empty),
                    modifier = Modifier.padding(top = 16.dp),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            } else {
                Column(modifier = Modifier.padding(top = 8.dp)) {
                    state.transactions.forEachIndexed { index, transaction ->
                        TransactionRow(transaction)

                        if (index < state.transactions.lastIndex) {
                            HorizontalDivider(
                                color = MaterialTheme.colorScheme.outlineVariant,
                            )
                        }
                    }
                }

                if (state.error != null) {
                    SectionError(
                        message = state.error,
                        onRetry = onRetry,
                        modifier = Modifier.padding(top = 16.dp),
                    )
                }

                if (state.totalPages > 1) {
                    Pagination(
                        page = state.page,
                        totalPages = state.totalPages,
                        onPrevious = onPreviousPage,
                        onNext = onNextPage,
                        modifier = Modifier.padding(top = 20.dp),
                    )
                }
            }
        }
    }
}

@Composable
private fun TransactionRow(transaction: BalanceTransaction) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 12.dp),
    ) {
        Row(modifier = Modifier.fillMaxWidth()) {
            Text(
                text = transaction.description ?: transactionLabel(transaction.type),
                modifier = Modifier.weight(1f),
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.SemiBold,
            )
            Text(
                text = formatMoney(transaction.amountMinor),
                modifier = Modifier.padding(start = 16.dp),
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.SemiBold,
            )
        }

        Text(
            text = formatDateTime(transaction.createdAt),
            modifier = Modifier.padding(top = 4.dp),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun transactionLabel(type: String): String = when (type) {
    "funding_credit" -> stringResource(R.string.balance_transaction_funding_credit)
    "ai_usage_debit" -> stringResource(R.string.balance_transaction_ai_usage_debit)
    else -> type
}

/**
 * The gateway's transparency ledger. The web client uses a table; on a phone
 * the same fields read better as stacked rows with the cost kept prominent.
 */
@Composable
fun AiUsageAuditSection(
    state: AiUsageUiState,
    onRetry: () -> Unit,
) {
    Column(modifier = Modifier.padding(top = 40.dp)) {
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)

        Eyebrow(
            text = stringResource(R.string.audit_eyebrow),
            modifier = Modifier.padding(top = 28.dp),
        )

        Text(
            text = stringResource(R.string.audit_title),
            modifier = Modifier.padding(top = 10.dp),
            style = MaterialTheme.typography.headlineSmall,
            fontWeight = FontWeight.Bold,
        )

        Text(
            text = stringResource(R.string.audit_description),
            modifier = Modifier.padding(top = 10.dp),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        when (state) {
            AiUsageUiState.Loading -> CompactLoader(
                label = stringResource(R.string.audit_loading),
                modifier = Modifier.padding(top = 20.dp),
            )

            is AiUsageUiState.Error -> SectionError(
                message = stringResource(R.string.audit_error),
                onRetry = onRetry,
                modifier = Modifier.padding(top = 20.dp),
            )

            is AiUsageUiState.Content -> {
                if (state.usages.isEmpty()) {
                    EmptyAuditState(modifier = Modifier.padding(top = 20.dp))
                } else {
                    UsageLedger(
                        usages = state.usages,
                        modifier = Modifier.padding(top = 20.dp),
                    )
                }
            }
        }
    }
}

@Composable
private fun UsageLedger(
    usages: List<AiUsage>,
    modifier: Modifier = Modifier,
) {
    Surface(
        modifier = modifier.fillMaxWidth(),
        shape = MaterialTheme.shapes.medium,
        color = MaterialTheme.colorScheme.surfaceContainerLow,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
    ) {
        Column {
            usages.forEachIndexed { index, usage ->
                UsageRow(usage)

                if (index < usages.lastIndex) {
                    HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                }
            }
        }
    }
}

@Composable
private fun UsageRow(usage: AiUsage) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 14.dp, vertical = 12.dp),
        verticalAlignment = Alignment.Top,
    ) {
        Icon(
            imageVector = Icons.Outlined.Memory,
            contentDescription = null,
            modifier = Modifier.size(15.dp),
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        Spacer(Modifier.width(8.dp))

        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = usage.model,
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.SemiBold,
            )
            Text(
                text = usage.provider,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Text(
                text = stringResource(
                    R.string.audit_tokens,
                    formatCount(usage.promptTokens),
                    formatCount(usage.completionTokens),
                ),
                modifier = Modifier.padding(top = 4.dp),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Text(
                text = formatShortDateTime(usage.createdAt),
                modifier = Modifier.padding(top = 2.dp),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }

        Text(
            text = formatMoney(usage.costMinor),
            modifier = Modifier.padding(start = 12.dp),
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.tertiary,
        )
    }
}

@Composable
private fun EmptyAuditState(modifier: Modifier = Modifier) {
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
                imageVector = Icons.Outlined.Bolt,
                contentDescription = null,
                modifier = Modifier.size(24.dp),
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Text(
                text = stringResource(R.string.audit_empty),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
            )
        }
    }
}

@Composable
private fun SectionError(
    message: String,
    onRetry: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Column(modifier = modifier) {
        Text(
            text = message,
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
}
