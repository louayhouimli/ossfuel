package com.example.ossfuel.data

import com.example.ossfuel.AppLog
import java.io.IOException
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.DeserializationStrategy
import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody

/**
 * The single HTTP boundary. It attaches the bearer token, tags requests as
 * coming from Android, and turns problem responses into [ApiException]s.
 *
 * Endpoint paths live in the repositories so this class stays a transport.
 */
class ApiClient(
    private val baseUrl: String,
    private val tokenProvider: () -> String?,
    private val onUnauthorized: () -> Unit,
) {
    val json = Json {
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

    suspend fun <T> get(path: String, deserializer: DeserializationStrategy<T>): T =
        decode(execute(getRequest(path)), deserializer)

    suspend fun <T> post(
        path: String,
        body: String?,
        deserializer: DeserializationStrategy<T>,
    ): T = decode(execute(postRequest(path, body)), deserializer)

    suspend fun delete(path: String) {
        execute(deleteRequest(path))
    }

    private fun getRequest(path: String): Request =
        Request.Builder()
            .url(url(path))
            .get()
            .build()

    private fun postRequest(path: String, body: String?): Request =
        Request.Builder()
            .url(url(path))
            .post(body?.toRequestBody(JSON_MEDIA_TYPE) ?: EMPTY_BODY)
            .build()

    private fun deleteRequest(path: String): Request =
        Request.Builder()
            .url(url(path))
            .delete()
            .build()

    private fun url(path: String): String = baseUrl.trimEnd('/') + path

    private suspend fun execute(request: Request): String = withContext(Dispatchers.IO) {
        try {
            client.newCall(request).execute().use { response ->
                if (response.code == 401) {
                    onUnauthorized()
                    throw ApiException(401, "Your sign-in expired. Please sign in again.")
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

    private fun <T> decode(body: String, deserializer: DeserializationStrategy<T>): T {
        if (body.isBlank()) {
            throw ApiException(0, "OSSFuel returned an empty response.")
        }

        return try {
            json.decodeFromString(deserializer, body)
        } catch (exception: SerializationException) {
            AppLog.warn("could not read the API response", exception)
            throw ApiException(0, "OSSFuel returned an unexpected response.")
        }
    }

    private fun readError(body: String?): String {
        if (body.isNullOrBlank()) {
            return "Something went wrong. Try again."
        }

        return try {
            val problem = json.decodeFromString(ProblemDetails.serializer(), body)
            problem.detail
                ?: problem.errors?.values?.firstOrNull()?.firstOrNull()
                ?: problem.title
                ?: "Something went wrong. Try again."
        } catch (exception: Exception) {
            "Something went wrong. Try again."
        }
    }

    private companion object {
        const val CLIENT_HEADER = "X-OSSFuel-Client"
        val JSON_MEDIA_TYPE = "application/json; charset=utf-8".toMediaType()
        val EMPTY_BODY = ByteArray(0).toRequestBody(null)
    }
}
