package com.example.ossfuel.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import androidx.navigation.toRoute
import com.example.ossfuel.data.AuthUser
import com.example.ossfuel.data.ProjectRepository
import com.example.ossfuel.ui.components.OssfuelTopBar
import com.example.ossfuel.ui.projects.ImportScreen
import com.example.ossfuel.ui.projects.ImportViewModel
import com.example.ossfuel.ui.projects.ProjectDetailScreen
import com.example.ossfuel.ui.projects.ProjectDetailViewModel
import com.example.ossfuel.ui.projects.ProjectsScreen
import com.example.ossfuel.ui.projects.ProjectsViewModel
import kotlinx.serialization.Serializable

@Serializable
data object ProjectsRoute

@Serializable
data class ProjectDetailRoute(val projectId: String)

@Serializable
data object ImportRoute

/**
 * The signed-in app. [ProjectsRoute] sits at the root of the stack, so import
 * and detail screens always have a list to return to.
 */
@Composable
fun OssfuelNavHost(
    user: AuthUser,
    repository: ProjectRepository,
    onSignOut: () -> Unit,
) {
    val navController = rememberNavController()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        OssfuelTopBar(
            user = user,
            onImport = {
                navController.navigate(ImportRoute) { launchSingleTop = true }
            },
            onSignOut = onSignOut,
        )

        NavHost(
            navController = navController,
            startDestination = ProjectsRoute,
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .navigationBarsPadding(),
        ) {
            composable<ProjectsRoute> {
                val viewModel: ProjectsViewModel = viewModel(
                    factory = ProjectsViewModel.Factory(repository),
                )
                val state by viewModel.state.collectAsStateWithLifecycle()
                // The first load comes from the ViewModel's init. On every
                // return to this destination we refresh, so imports and
                // deletions show up without a manual pull.
                val hasLoaded = rememberSaveable { mutableStateOf(false) }

                LaunchedEffect(Unit) {
                    if (hasLoaded.value) {
                        viewModel.refresh()
                    } else {
                        hasLoaded.value = true
                    }
                }

                ProjectsScreen(
                    state = state,
                    onRetry = viewModel::retry,
                    onRefresh = viewModel::refresh,
                    onPreviousPage = viewModel::previousPage,
                    onNextPage = viewModel::nextPage,
                    onProjectClick = { project ->
                        navController.navigate(ProjectDetailRoute(project.id))
                    },
                    onImport = {
                        navController.navigate(ImportRoute) { launchSingleTop = true }
                    },
                )
            }

            composable<ProjectDetailRoute> { entry ->
                val route = entry.toRoute<ProjectDetailRoute>()
                val viewModel: ProjectDetailViewModel = viewModel(
                    factory = ProjectDetailViewModel.Factory(repository, route.projectId),
                )
                val state by viewModel.state.collectAsStateWithLifecycle()

                ProjectDetailScreen(
                    state = state,
                    userId = user.id,
                    onBack = { navController.popBackStack() },
                    onRetry = viewModel::retry,
                    onRefresh = viewModel::refresh,
                    onDelete = viewModel::delete,
                    onDeleted = { navController.popBackStack() },
                )
            }

            composable<ImportRoute> {
                val viewModel: ImportViewModel = viewModel(
                    factory = ImportViewModel.Factory(repository),
                )
                val state by viewModel.state.collectAsStateWithLifecycle()

                ImportScreen(
                    state = state,
                    onRetry = viewModel::retry,
                    onQueryChange = viewModel::setQuery,
                    onImport = viewModel::importProject,
                    onImported = { projectId ->
                        viewModel.consumeNavigation()
                        navController.navigate(ProjectDetailRoute(projectId)) {
                            popUpTo<ProjectsRoute>()
                        }
                    },
                )
            }
        }
    }
}
