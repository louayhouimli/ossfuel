package com.example.ossfuel

import android.app.Application
import android.content.Context
import coil3.ImageLoader
import coil3.PlatformContext
import coil3.SingletonImageLoader
import coil3.network.okhttp.OkHttpNetworkFetcherFactory
import coil3.request.crossfade
import com.example.ossfuel.data.ApiClient
import com.example.ossfuel.data.AuthEvents
import com.example.ossfuel.data.AuthRepository
import com.example.ossfuel.data.NetworkProjectRepository
import com.example.ossfuel.data.ProjectRepository
import com.example.ossfuel.data.TokenStore

class OssfuelApplication : Application(), SingletonImageLoader.Factory {
    lateinit var container: AppContainer
        private set

    override fun onCreate() {
        super.onCreate()
        container = AppContainer(this)
    }

    override fun newImageLoader(context: PlatformContext): ImageLoader =
        ImageLoader.Builder(context)
            .components { add(OkHttpNetworkFetcherFactory()) }
            .crossfade(true)
            .build()
}

class AppContainer(context: Context) {
    private val tokenStore = TokenStore(context)

    val authEvents = AuthEvents()

    private val apiClient = ApiClient(
        baseUrl = BuildConfig.API_BASE_URL,
        tokenProvider = { tokenStore.accessToken },
        onUnauthorized = {
            tokenStore.clearToken()
            authEvents.notifySignedOut("Your sign-in expired. Please sign in again.")
        },
    )

    val authRepository = AuthRepository(
        api = apiClient,
        tokenStore = tokenStore,
        baseUrl = BuildConfig.API_BASE_URL,
    )

    val projectRepository: ProjectRepository = NetworkProjectRepository(apiClient)
}
