package com.example.ossfuel.data

import kotlinx.serialization.Serializable

@Serializable
data class AuthUser(
    val id: String,
    val login: String,
    val name: String? = null,
    val email: String? = null,
    val avatarUrl: String? = null,
)

@Serializable
data class MobileAuthResponse(
    val accessToken: String,
    val expiresAt: String,
    val user: AuthUser,
)

@Serializable
data class MobileExchangeRequest(
    val code: String,
)

@Serializable
data class ProblemDetails(
    val title: String? = null,
    val detail: String? = null,
    val errors: Map<String, List<String>>? = null,
)
