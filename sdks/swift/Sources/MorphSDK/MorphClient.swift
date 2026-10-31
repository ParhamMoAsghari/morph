import Foundation

/// Stub Morph platform client. Full codegen comes later.
public final class MorphClient: @unchecked Sendable {
    public let baseURL: URL
    public var accessToken: String?

    public init(baseURL: URL, accessToken: String? = nil) {
        self.baseURL = baseURL
        self.accessToken = accessToken
    }

    /// Placeholder for REST data CRUD (`/data/v1/...`).
    public func listRecords(environmentId: String, entity: String) async throws -> Data {
        try await request(method: "GET", path: "/data/v1/\(environmentId)/\(entity)")
    }

    /// Placeholder for process start (`/v1/environments/{id}/processes/{slug}/run`).
    public func runProcess(environmentId: String, slug: String, body: Data? = nil) async throws -> Data {
        try await request(
            method: "POST",
            path: "/v1/environments/\(environmentId)/processes/\(slug)/run",
            body: body
        )
    }

    /// Placeholder for function invoke (`/v1/environments/{id}/functions/{slug}/invoke`).
    public func invokeFunction(environmentId: String, slug: String, body: Data? = nil) async throws -> Data {
        try await request(
            method: "POST",
            path: "/v1/environments/\(environmentId)/functions/\(slug)/invoke",
            body: body
        )
    }

    private func request(method: String, path: String, body: Data? = nil) async throws -> Data {
        var components = URLComponents(url: baseURL, resolvingAgainstBaseURL: false)!
        let basePath = components.path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        let suffix = path.hasPrefix("/") ? String(path.dropFirst()) : path
        components.path = "/" + [basePath, suffix].filter { !$0.isEmpty }.joined(separator: "/")

        var request = URLRequest(url: components.url!)
        request.httpMethod = method
        request.httpBody = body
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        if let accessToken {
            request.setValue("Bearer \(accessToken)", forHTTPHeaderField: "Authorization")
        }

        let (data, response) = try await URLSession.shared.data(for: request)
        guard let http = response as? HTTPURLResponse, (200..<300).contains(http.statusCode) else {
            throw URLError(.badServerResponse)
        }
        return data
    }
}
