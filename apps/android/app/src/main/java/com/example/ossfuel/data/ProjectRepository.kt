package com.example.ossfuel.data

import kotlinx.serialization.builtins.ListSerializer
import kotlinx.serialization.encodeToString

interface ProjectRepository {
    suspend fun list(page: Int, pageSize: Int): ProjectList

    suspend fun get(id: String): Project

    suspend fun availableRepositories(): List<AvailableRepository>

    suspend fun importProject(fullName: String): Project

    suspend fun refresh(id: String): Project

    suspend fun delete(id: String)
}

class NetworkProjectRepository(private val api: ApiClient) : ProjectRepository {
    override suspend fun list(page: Int, pageSize: Int): ProjectList =
        api.get(
            "$PROJECTS_PATH?page=$page&pageSize=$pageSize",
            ProjectList.serializer(),
        )

    override suspend fun get(id: String): Project =
        api.get("$PROJECTS_PATH/$id", Project.serializer())

    override suspend fun availableRepositories(): List<AvailableRepository> =
        api.get(
            AVAILABLE_REPOSITORIES_PATH,
            ListSerializer(AvailableRepository.serializer()),
        )

    override suspend fun importProject(fullName: String): Project {
        val body = api.json.encodeToString(
            CreateProjectRequest.serializer(),
            CreateProjectRequest(fullName),
        )

        return api.post(PROJECTS_PATH, body, Project.serializer())
    }

    override suspend fun refresh(id: String): Project =
        api.post("$PROJECTS_PATH/$id/refresh", null, Project.serializer())

    override suspend fun delete(id: String) {
        api.delete("$PROJECTS_PATH/$id")
    }

    private companion object {
        const val PROJECTS_PATH = "/api/v1/projects"
        const val AVAILABLE_REPOSITORIES_PATH = "/api/v1/repos/available"
    }
}
