package com.example.ossfuel.ui.format

import java.text.NumberFormat
import java.time.Duration
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle

private val dateFormatter: DateTimeFormatter =
    DateTimeFormatter.ofLocalizedDate(FormatStyle.MEDIUM)

fun formatCount(value: Int): String = NumberFormat.getIntegerInstance().format(value)

/**
 * The API serializes `DateTimeOffset` as `+00:00`, which [Instant.parse]
 * rejects because it only accepts a trailing `Z`. Fall back to
 * [OffsetDateTime] so both shapes work.
 */
private fun parseTimestamp(isoTimestamp: String): Instant? =
    runCatching { Instant.parse(isoTimestamp) }
        .recoverCatching { OffsetDateTime.parse(isoTimestamp).toInstant() }
        .getOrNull()

fun formatDate(isoTimestamp: String): String {
    val instant = parseTimestamp(isoTimestamp) ?: return isoTimestamp

    return dateFormatter
        .withZone(ZoneId.systemDefault())
        .format(instant)
}

/**
 * Mirrors the web's `LiveTime` treatment: short relative labels while the
 * snapshot is recent, then the calendar date once it is older than a week.
 */
fun formatRelativeTime(
    isoTimestamp: String,
    now: Instant = Instant.now(),
): String {
    val instant = parseTimestamp(isoTimestamp) ?: return isoTimestamp

    val seconds = Duration.between(instant, now).seconds

    return when {
        seconds < 60 -> "Just now"
        seconds < 3_600 -> "${seconds / 60}m ago"
        seconds < 86_400 -> "${seconds / 3_600}h ago"
        seconds < 604_800 -> "${seconds / 86_400}d ago"
        else -> formatDate(isoTimestamp)
    }
}
