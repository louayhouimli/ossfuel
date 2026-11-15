package com.example.ossfuel

import com.example.ossfuel.ui.format.formatCount
import com.example.ossfuel.ui.format.formatDate
import com.example.ossfuel.ui.format.formatRelativeTime
import java.time.Instant
import org.junit.Assert.assertEquals
import org.junit.Test

class FormattersTest {
    private val now = Instant.parse("2026-02-01T12:00:00Z")

    @Test
    fun formatCountKeepsSmallNumbersReadable() {
        assertEquals("0", formatCount(0))
        assertEquals("7", formatCount(7))
        assertEquals("999", formatCount(999))
    }

    @Test
    fun relativeTimeUsesMinutesUnderAnHour() {
        assertEquals(
            "5m ago",
            formatRelativeTime("2026-02-01T11:55:00Z", now),
        )
    }

    @Test
    fun relativeTimeUsesHoursUnderADay() {
        assertEquals(
            "3h ago",
            formatRelativeTime("2026-02-01T09:00:00Z", now),
        )
    }

    @Test
    fun relativeTimeUsesDaysUnderAWeek() {
        assertEquals(
            "2d ago",
            formatRelativeTime("2026-01-30T12:00:00Z", now),
        )
    }

    @Test
    fun relativeTimeFallsBackToTheCalendarDateAfterAWeek() {
        val old = "2026-01-20T12:00:00Z"

        assertEquals(formatDate(old), formatRelativeTime(old, now))
    }

    @Test
    fun relativeTimeTreatsTheLastMinuteAsJustNow() {
        assertEquals(
            "Just now",
            formatRelativeTime("2026-02-01T11:59:30Z", now),
        )
    }

    @Test
    fun relativeTimeTreatsAHeadClockAsJustNow() {
        assertEquals(
            "Just now",
            formatRelativeTime("2026-02-01T12:05:00Z", now),
        )
    }

    @Test
    fun unparseableTimestampsAreReturnedUnchanged() {
        assertEquals("not-a-date", formatRelativeTime("not-a-date", now))
        assertEquals("not-a-date", formatDate("not-a-date"))
    }

    @Test
    fun parsesTimestampsWithAnExplicitUtcOffset() {
        assertEquals(
            "5m ago",
            formatRelativeTime("2026-02-01T11:55:00+00:00", now),
        )
    }

    @Test
    fun parsesNonUtcOffsets() {
        // The same instant as `now`, written two hours ahead.
        assertEquals(
            "Just now",
            formatRelativeTime("2026-02-01T14:00:00+02:00", now),
        )
    }

    @Test
    fun offsetAndUTCTimestampsFormatTheSame() {
        assertEquals(
            formatDate("2026-02-01T12:00:00Z"),
            formatDate("2026-02-01T12:00:00+00:00"),
        )
    }

    @Test
    fun parsesFractionalSecondsFromTheApi() {
        // ASP.NET serializes `DateTimeOffset` with up to seven fractional digits.
        assertEquals(
            "3h ago",
            formatRelativeTime("2026-02-01T08:30:00.1234567+00:00", now),
        )
    }
}
