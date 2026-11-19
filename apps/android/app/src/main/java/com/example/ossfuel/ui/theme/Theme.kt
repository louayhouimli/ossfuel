package com.example.ossfuel.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.graphics.Color

/**
 * Material has no warning role. The key-creation banner borrows the web's
 * amber treatment, so the value follows the selected palette here rather than
 * reaching for `error`, which already means destructive.
 */
val LocalWarningColor = staticCompositionLocalOf { Color.Unspecified }

@Composable
fun OssfuelTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit,
) {
    CompositionLocalProvider(
        LocalWarningColor provides if (darkTheme) InkWarning else PaperWarning,
    ) {
        MaterialTheme(
            colorScheme = if (darkTheme) DarkColors else LightColors,
            typography = OssfuelTypography,
            shapes = OssfuelShapes,
            content = content,
        )
    }
}
