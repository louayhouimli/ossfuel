package com.example.ossfuel.ui.format

import java.math.BigDecimal
import java.text.NumberFormat
import java.time.Duration
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle
import java.util.Currency
import java.util.Locale

private val dateFormatter: DateTimeFormatter =
    DateTimeFormatter.ofLocalizedDate(FormatStyle.MEDIUM)

private val dateTimeFormatter: DateTimeFormatter =
    DateTimeFormatter.ofLocalizedDateTime(FormatStyle.MEDIUM, FormatStyle.SHORT)

private val shortDateTimeFormatter: DateTimeFormatter =
    DateTimeFormatter.ofLocalizedDateTime(FormatStyle.SHORT, FormatStyle.SHORT)

fun formatCount(value: Int): String = NumberFormat.getIntegerInstance().format(value)

/**
 * Formats minor units as a localized currency amount, mirroring the web's
 * `formatMoney`. The API records cents, so the value is scaled without a
 * floating-point round trip.
 */
fun formatMoney(amountMinor: Long, currency: String = "usd"): String {
    val formatter = NumberFormat.getCurrencyInstance()

    runCatching {
        formatter.currency = Currency.getInstance(currency.uppercase(Locale.ROOT))
    }

    return formatter.format(BigDecimal.valueOf(amountMinor, 2))
}

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

/** Medium date plus short time, matching the web's funding-history stamps. */
fun formatDateTime(isoTimestamp: String): String {
    val instant = parseTimestamp(isoTimestamp) ?: return isoTimestamp

    return dateTimeFormatter
        .withZone(ZoneId.systemDefault())
        .format(instant)
}

/** Short date plus short time, matching the web's audit-log stamps. */
fun formatShortDateTime(isoTimestamp: String): String {
    val instant = parseTimestamp(isoTimestamp) ?: return isoTimestamp

    return shortDateTimeFormatter
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
