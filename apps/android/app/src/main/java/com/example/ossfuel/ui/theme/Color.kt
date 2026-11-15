package com.example.ossfuel.ui.theme

import androidx.compose.ui.graphics.Color

/**
 * The OSSFuel palette, lifted from the web app's `globals.css` tokens.
 * Warm paper and ink rather than Material's neutral greys, with hairlines
 * instead of shadows.
 */
internal val Paper = Color(0xFFF2F1EF)
internal val Ink = Color(0xFF0A0A0B)
internal val PaperCard = Color(0xFFFCFCFB)
internal val PaperSecondary = Color(0xFFF7F7F6)
internal val PaperMuted = Color(0xFFEFEEEE)
internal val PaperAccent = Color(0xFFEBEBEA)
internal val PaperBorder = Color(0xFFDBDAD9)
internal val PaperMutedForeground = Color(0xFF69696A)
internal val PaperDestructive = Color(0xFFC23B44)

internal val InkCard = Color(0xFF0F0F10)
internal val InkSecondary = Color(0xFF141415)
internal val InkMuted = Color(0xFF181819)
internal val InkAccent = Color(0xFF1E1E21)
internal val InkBorder = Color(0xFF35353A)
internal val InkMutedForeground = Color(0xFF9E9E9F)
internal val InkDestructive = Color(0xFFF17074)

internal val LightColors = androidx.compose.material3.lightColorScheme(
    primary = Ink,
    onPrimary = Paper,
    primaryContainer = PaperAccent,
    onPrimaryContainer = Ink,
    inversePrimary = Paper,

    secondary = PaperSecondary,
    onSecondary = Color(0xFF46464A),
    secondaryContainer = PaperMuted,
    onSecondaryContainer = Ink,

    tertiary = Ink,
    onTertiary = Paper,

    background = Paper,
    onBackground = Ink,

    surface = Paper,
    onSurface = Ink,
    surfaceVariant = PaperMuted,
    onSurfaceVariant = PaperMutedForeground,
    surfaceTint = Ink,

    surfaceContainerLowest = Color(0xFFFFFFFF),
    surfaceContainerLow = PaperCard,
    surfaceContainer = PaperSecondary,
    surfaceContainerHigh = PaperMuted,
    surfaceContainerHighest = PaperAccent,

    inverseSurface = Ink,
    inverseOnSurface = Paper,

    outline = PaperBorder,
    outlineVariant = Color(0xFFE5E4E1),

    error = PaperDestructive,
    onError = Color(0xFFFFFFFF),
    errorContainer = Color(0xFFF7D7D9),
    onErrorContainer = Color(0xFF5A1A1E),
)

internal val DarkColors = androidx.compose.material3.darkColorScheme(
    primary = Paper,
    onPrimary = Ink,
    primaryContainer = InkAccent,
    onPrimaryContainer = Paper,
    inversePrimary = Ink,

    secondary = InkSecondary,
    onSecondary = Color(0xFFD6D5D2),
    secondaryContainer = InkMuted,
    onSecondaryContainer = Paper,

    tertiary = Paper,
    onTertiary = Ink,

    background = Ink,
    onBackground = Paper,

    surface = Ink,
    onSurface = Paper,
    surfaceVariant = InkMuted,
    onSurfaceVariant = InkMutedForeground,
    surfaceTint = Paper,

    surfaceContainerLowest = Color(0xFF070708),
    surfaceContainerLow = InkCard,
    surfaceContainer = InkSecondary,
    surfaceContainerHigh = InkMuted,
    surfaceContainerHighest = InkAccent,

    inverseSurface = Paper,
    inverseOnSurface = Ink,

    outline = InkBorder,
    outlineVariant = Color(0xFF26262A),

    error = InkDestructive,
    onError = Color(0xFF3A0A0D),
    errorContainer = Color(0xFF3A1418),
    onErrorContainer = Color(0xFFFFD9DB),
)
