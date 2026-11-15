package com.example.ossfuel

import com.example.ossfuel.data.AiUsage
import com.example.ossfuel.data.ApiClient
import com.example.ossfuel.data.AvailableRepository
import com.example.ossfuel.data.BalanceTransactionList
import com.example.ossfuel.data.Project
import com.example.ossfuel.data.ProjectList
import kotlinx.serialization.builtins.ListSerializer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class ProjectModelsTest {
    private val json = ApiClient("http://localhost", { null }, {}).json

    @Test
    fun projectDecodesAllFieldsAndIgnoresUnknownKeys() {
        val project = json.decodeFromString(
            Project.serializer(),
            """
            {
              "id": "project-1",
              "fullName": "octocat/ossfuel",
              "ownerLogin": "octocat",
              "name": "ossfuel",
              "description": "An accounting layer.",
              "htmlUrl": "https://github.com/octocat/ossfuel",
              "homepageUrl": "https://ossfuel.example",
              "language": "Kotlin",
              "stars": 1200,
              "forks": 30,
              "openIssues": 4,
              "defaultBranch": "main",
              "topics": ["funding", "ai"],
              "createdAt": "2025-06-01T12:00:00Z",
              "lastSyncedAt": "2026-02-01T12:00:00Z",
              "ownerUserId": "user-1",
              "unexpected": "ignored"
            }
            """.trimIndent(),
        )

        assertEquals("project-1", project.id)
        assertEquals("octocat/ossfuel", project.fullName)
        assertEquals(1200, project.stars)
        assertEquals(30, project.forks)
        assertEquals(4, project.openIssues)
        assertEquals(listOf("funding", "ai"), project.topics)
        assertEquals("https://ossfuel.example", project.homepageUrl)
        assertEquals("user-1", project.ownerUserId)
    }

    @Test
    fun projectUsesDefaultsForOmittedOptionalFields() {
        val project = json.decodeFromString(
            Project.serializer(),
            """
            {
              "id": "project-1",
              "fullName": "octocat/ossfuel",
              "ownerLogin": "octocat",
              "name": "ossfuel",
              "htmlUrl": "https://github.com/octocat/ossfuel",
              "createdAt": "2025-06-01T12:00:00Z",
              "lastSyncedAt": "2026-02-01T12:00:00Z",
              "ownerUserId": "user-1"
            }
            """.trimIndent(),
        )

        assertNull(project.description)
        assertNull(project.language)
        assertNull(project.homepageUrl)
        assertNull(project.defaultBranch)
        assertEquals(0, project.stars)
        assertEquals(0, project.forks)
        assertEquals(0, project.openIssues)
        assertEquals(emptyList<String>(), project.topics)
    }

    @Test
    fun projectListDecodesPagination() {
        val list = json.decodeFromString(
            ProjectList.serializer(),
            """
            {
              "items": [
                {
                  "id": "project-1",
                  "fullName": "octocat/ossfuel",
                  "ownerLogin": "octocat",
                  "name": "ossfuel",
                  "stars": 12
                }
              ],
              "page": 2,
              "pageSize": 12,
              "totalCount": 25
            }
            """.trimIndent(),
        )

        assertEquals(1, list.items.size)
        assertEquals("ossfuel", list.items.first().name)
        assertEquals(2, list.page)
        assertEquals(12, list.pageSize)
        assertEquals(25, list.totalCount)
        assertEquals(3, list.totalPages)
    }

    @Test
    fun projectListComputesTotalPages() {
        assertEquals(1, ProjectList(page = 1, pageSize = 12, totalCount = 0).totalPages)
        assertEquals(1, ProjectList(page = 1, pageSize = 12, totalCount = 12).totalPages)
        assertEquals(2, ProjectList(page = 1, pageSize = 12, totalCount = 13).totalPages)
        assertEquals(1, ProjectList(page = 1, pageSize = 0, totalCount = 5).totalPages)
    }

    @Test
    fun availableRepositoriesDecodeAsAList() {
        val repositories = json.decodeFromString(
            ListSerializer(AvailableRepository.serializer()),
            """
            [
              {
                "id": 42,
                "fullName": "octocat/ossfuel",
                "ownerLogin": "octocat",
                "name": "ossfuel",
                "description": "An accounting layer.",
                "language": "Kotlin",
                "stars": 120,
                "alreadyImported": true
              }
            ]
            """.trimIndent(),
        )

        assertEquals(1, repositories.size)
        assertEquals(42L, repositories.first().id)
        assertEquals(true, repositories.first().alreadyImported)
    }

    @Test
    fun balanceTransactionsDecodeTheSponsorAndPaging() {
        val history = json.decodeFromString(
            BalanceTransactionList.serializer(),
            """
            {
              "items": [
                {
                  "id": "transaction-1",
                  "amountMinor": 2500,
                  "type": "funding_credit",
                  "status": "succeeded",
                  "description": "Sponsor credit",
                  "sponsor": {
                    "id": "sponsor-1",
                    "login": "octocat",
                    "name": "The Octocat",
                    "avatarUrl": "https://avatars.example/octocat.png"
                  },
                  "createdAt": "2026-02-01T12:00:00+00:00"
                }
              ],
              "page": 1,
              "pageSize": 10,
              "totalCount": 11
            }
            """.trimIndent(),
        )

        assertEquals(1, history.items.size)
        assertEquals(2500, history.items.first().amountMinor)
        assertEquals("octocat", history.items.first().sponsor?.login)
        assertEquals(2, history.totalPages)
    }

    @Test
    fun aiUsageDecodesTheGatewayLedger() {
        val usages = json.decodeFromString(
            ListSerializer(AiUsage.serializer()),
            """
            [
              {
                "id": "usage-1",
                "projectId": "project-1",
                "apiKeyId": "key-1",
                "model": "gpt-4o-mini",
                "provider": "openai",
                "promptTokens": 1234,
                "completionTokens": 567,
                "totalTokens": 1801,
                "costMinor": 42,
                "status": "succeeded",
                "createdAt": "2026-02-01T12:00:00+00:00"
              }
            ]
            """.trimIndent(),
        )

        assertEquals(1, usages.size)
        assertEquals("gpt-4o-mini", usages.first().model)
        assertEquals(1801, usages.first().totalTokens)
        assertEquals(42L, usages.first().costMinor)
    }
}
