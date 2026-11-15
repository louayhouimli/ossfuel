package com.example.ossfuel

import android.app.Application
import android.content.Context
import com.example.ossfuel.data.ApiClient
import com.example.ossfuel.data.AuthRepository
import com.example.ossfuel.data.TokenStore

class OssfuelApplication : Application() {
    lateinit var container: AppContainer
        private set

    override fun onCreate() {
        super.onCreate()
        container = AppContainer(this)
    }
}

class AppContainer(context: Context) {
    private val tokenStore = TokenStore(context)

    private val apiClient = ApiClient(
        baseUrl = BuildConfig.API_BASE_URL,
        tokenProvider = { tokenStore.accessToken },
        onUnauthorized = { tokenStore.clearToken() },
    )

    val authRepository = AuthRepository(
        api = apiClient,
        tokenStore = tokenStore,
        baseUrl = BuildConfig.API_BASE_URL,
    )
}
