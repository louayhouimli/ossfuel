package com.example.ossfuel

import android.content.Intent
import android.net.Uri
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.browser.customtabs.CustomTabsIntent
import androidx.compose.runtime.getValue
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.example.ossfuel.ui.AuthViewModel
import com.example.ossfuel.ui.OssfuelAppRoot
import com.example.ossfuel.ui.theme.OssfuelTheme

class MainActivity : ComponentActivity() {
    private val container: AppContainer
        get() = (application as OssfuelApplication).container

    private val viewModel: AuthViewModel by viewModels {
        AuthViewModel.Factory(container.authRepository, container.authEvents)
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        if (savedInstanceState == null) {
            AppLog.debug("launch deep link: ${describe(intent?.data)}")
            viewModel.handleDeepLink(intent?.data)
        }

        setContent {
            OssfuelTheme {
                val state by viewModel.state.collectAsStateWithLifecycle()

                OssfuelAppRoot(
                    state = state,
                    projectRepository = container.projectRepository,
                    onSignIn = ::openSignIn,
                    onSignOut = viewModel::signOut,
                )
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        AppLog.debug("new deep link: ${describe(intent.data)}")
        viewModel.handleDeepLink(intent.data)
    }

    private fun openSignIn() {
        AppLog.debug("opening GitHub sign-in in a Custom Tab")
        val signInUrl = viewModel.beginSignIn()

        CustomTabsIntent.Builder()
            .setShowTitle(true)
            .build()
            .launchUrl(this, Uri.parse(signInUrl))
    }

    private fun describe(uri: Uri?): String = uri?.let {
        "scheme=${it.scheme} host=${it.host} path=${it.path} " +
            "hasState=${it.getQueryParameter("state") != null} " +
            "hasCode=${it.getQueryParameter("code") != null}"
    } ?: "none"
}
