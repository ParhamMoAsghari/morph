package com.morph.sdk

import java.net.URI
import java.net.http.HttpClient
import java.net.http.HttpRequest
import java.net.http.HttpResponse
import java.time.Duration

/**
 * Stub Morph platform client. Full codegen comes later.
 *
 * Auth: set [accessToken] for `Authorization: Bearer …`.
 */
class MorphClient(
    baseUrl: String,
    var accessToken: String? = null,
    private val http: HttpClient = HttpClient.newBuilder()
        .connectTimeout(Duration.ofSeconds(30))
        .build(),
) {
    private val baseUrl: String = baseUrl.trimEnd('/')

    /** Placeholder for REST data list (`/data/v1/...`). */
    fun listRecords(environmentId: String, entity: String): String =
        request("GET", "/data/v1/$environmentId/$entity")

    /** Placeholder for process start. */
    fun runProcess(environmentId: String, slug: String, jsonBody: String? = null): String =
        request("POST", "/v1/environments/$environmentId/processes/$slug/run", jsonBody)

    /** Placeholder for function invoke. */
    fun invokeFunction(environmentId: String, slug: String, jsonBody: String? = null): String =
        request("POST", "/v1/environments/$environmentId/functions/$slug/invoke", jsonBody)

    private fun request(method: String, path: String, jsonBody: String? = null): String {
        val builder = HttpRequest.newBuilder(URI.create("$baseUrl$path"))
            .timeout(Duration.ofSeconds(60))
            .header("Content-Type", "application/json")

        accessToken?.let { builder.header("Authorization", "Bearer $it") }

        when (method) {
            "GET" -> builder.GET()
            "POST" -> builder.POST(
                if (jsonBody == null) HttpRequest.BodyPublishers.noBody()
                else HttpRequest.BodyPublishers.ofString(jsonBody),
            )
            else -> error("Unsupported method: $method")
        }

        val response = http.send(builder.build(), HttpResponse.BodyHandlers.ofString())
        require(response.statusCode() in 200..299) {
            "Morph API ${response.statusCode()}: ${response.body()}"
        }
        return response.body()
    }
}
