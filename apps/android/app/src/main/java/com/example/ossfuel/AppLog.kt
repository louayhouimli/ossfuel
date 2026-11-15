package com.example.ossfuel

import android.util.Log

/**
 * Debug-only logging. Watch it with:
 *
 *     adb logcat -s OSSFuel
 */
object AppLog {
    const val TAG = "OSSFuel"

    fun debug(message: String) {
        if (BuildConfig.DEBUG) {
            Log.d(TAG, message)
        }
    }

    fun warn(message: String, throwable: Throwable? = null) {
        if (BuildConfig.DEBUG) {
            if (throwable == null) {
                Log.w(TAG, message)
            } else {
                Log.w(TAG, message, throwable)
            }
        }
    }
}
