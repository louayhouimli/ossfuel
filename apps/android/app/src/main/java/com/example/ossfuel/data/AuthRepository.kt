package com.example.ossfuel.data

import com.example.ossfuel.AppLog
import java.net.URLEncoder
import java.util.UUID

class AuthRepository(
    private val api: ApiClient,
    private val tokenStore: TokenStore,
    private val baseUrl: String,
) {
    suspend fun restoreSession(): AuthUser? {
        if (tokenStore.accessToken.isNullOrBlank()) {
            AppLog.debug("no stored token")
            return null
        }

        return try {
            AppLog.debug("validating stored token")
            api.me()
        } catch (exception: ApiException) {
            AppLog.warn("stored token was rejected (${exception.statusCode})")
            if (exception.statusCode == 401) {
                tokenStore.clearToken()
            }
            null
        } catch (exception: Exception) {
            AppLog.warn("could not validate stored token", exception)
            null
        }
    }

    fun beginSignIn(): String {
        AppLog.debug("starting a new sign-in")
        val state = UUID.randomUUID().toString()
        tokenStore.savePendingState(state)

        val returnUrl = "$REDIRECT_URI?state=" + encode(state)

        return baseUrl.trimEnd('/') + SIGN_IN_PATH + "?returnUrl=" + encode(returnUrl)
    }

    fun verifyAndClearState(state: String?): Boolean {
        val expected = tokenStore.pendingState
        tokenStore.clearPendingState()

        val matches = expected != null && expected == state
        AppLog.debug("sign-in state matched=$matches (pending=${expected != null})")

        return matches
    }

    suspend fun exchangeCode(code: String): AuthUser {
        val response = api.exchangeCode(code)
        tokenStore.saveToken(response.accessToken)
        AppLog.debug("stored the mobile access token")

        return response.user
    }

    fun signOut() {
        AppLog.debug("signing out")
        tokenStore.clearToken()
    }

    private fun encode(value: String): String = URLEncoder.encode(value, "UTF-8")

    companion object {
        const val REDIRECT_SCHEME = "ossfuel"
        const val REDIRECT_HOST = "auth"
        const val REDIRECT_PATH = "/callback"
        const val REDIRECT_URI = "ossfuel://auth/callback"

        private const val SIGN_IN_PATH = "/api/v1/auth/sign-in"
    }
}
