package com.example.ossfuel.data

class ApiException(
    val statusCode: Int,
    message: String,
    cause: Throwable? = null,
) : Exception(message, cause)
