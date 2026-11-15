package com.example.ossfuel.data

import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow

/**
 * Tells the app shell that the API rejected the current token.
 *
 * The client cannot navigate on its own, so it reports the rejection here and
 * [com.example.ossfuel.ui.AuthViewModel] moves the UI back to the sign-in gate.
 */
class AuthEvents {
    private val _signedOut = MutableSharedFlow<String>(extraBufferCapacity = 1)

    val signedOut: SharedFlow<String> = _signedOut.asSharedFlow()

    fun notifySignedOut(reason: String) {
        _signedOut.tryEmit(reason)
    }
}
