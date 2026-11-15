package com.example.ossfuel.data

import com.example.ossfuel.AppLog
import java.io.IOException
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.decodeFromString
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody

class ApiClient(
    private val baseUrl: String,
    private val tokenProvider: () -> String?,
    private val onUnauthorized: () -> Unit,
) {
    private val json = Json {
        ignoreUnknownKeys = true
        explicitNulls = false
    }

    private val client = OkHttpClient.Builder()
        .connectTimeout(20, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .addInterceptor { chain ->
            val token = tokenProvider()
            val request = chain.request().newBuilder()
                .header(CLIENT_HEADER, "android")
                .apply {
                    if (!token.isNullOrBlank()) {
                        header("Authorization", "Bearer $token")
                    }
                }
                .build()

            val response = chain.proceed(request)
            AppLog.debug("${request.method} ${request.url.encodedPath} -> ${response.code}")
            response
        }
        .build()

    suspend fun exchangeCode(code: String): MobileAuthResponse =
        json.decodeFromString(execute(post(EXCHANGE_PATH, json.encodeToString(MobileExchangeRequest(code)))))

    suspend fun me(): AuthUser =
        json.decodeFromString(execute(get(ME_PATH)))

    private fun get(path: String): Request =
        Request.Builder()
            .url(url(path))
            .get()
            .build()

    private fun post(path: String, body: String): Request =
        Request.Builder()
            .url(url(path))
            .post(body.toRequestBody(JSON_MEDIA_TYPE))
            .build()

    private fun url(path: String): String = baseUrl.trimEnd('/') + path

    private suspend fun execute(request: Request): String = withContext(Dispatchers.IO) {
        try {
            client.newCall(request).execute().use { response ->
                if (response.code == 401) {
                    onUnauthorized()
                    throw ApiException(401, "Your sign-in has expired. Please sign in again.")
                }

                if (!response.isSuccessful) {
                    throw ApiException(response.code, readError(response.body?.string()))
                }

                response.body?.string().orEmpty()
            }
        } catch (exception: IOException) {
            AppLog.warn("request failed: ${request.url.encodedPath}", exception)
            throw ApiException(
                0,
                "Can't reach OSSFuel. Check your connection and try again.",
                exception,
            )
        }
    }

    private fun readError(body: String?): String {
        if (body.isNullOrBlank()) {
            return "Sign-in failed. Please try again."
        }

        return try {
            val problem = json.decodeFromString<ProblemDetails>(body)
            problem.detail ?: problem.title ?: "Sign-in failed. Please try again."
        } catch (exception: Exception) {
            "Sign-in failed. Please try again."
        }
    }

    private companion object {
        const val EXCHANGE_PATH = "/api/v1/auth/mobile/exchange"
        const val ME_PATH = "/api/v1/auth/me"
        const val CLIENT_HEADER = "X-OSSFuel-Client"
        val JSON_MEDIA_TYPE = "application/json; charset=utf-8".toMediaType()
    }
}
