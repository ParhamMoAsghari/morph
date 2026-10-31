/// Stub Morph Flutter/Dart client. Full codegen comes later.
library;

class MorphClient {
  MorphClient({required this.baseUrl, this.accessToken});

  final String baseUrl;
  String? accessToken;

  /// Placeholder for REST data CRUD.
  Uri dataUri(String environmentId, String entity) =>
      Uri.parse('$baseUrl/data/v1/$environmentId/$entity');

  /// Placeholder for process start.
  Uri processRunUri(String environmentId, String slug) =>
      Uri.parse('$baseUrl/v1/environments/$environmentId/processes/$slug/run');

  /// Placeholder for function invoke.
  Uri functionInvokeUri(String environmentId, String slug) =>
      Uri.parse('$baseUrl/v1/environments/$environmentId/functions/$slug/invoke');

  Map<String, String> authHeaders() => {
        if (accessToken != null) 'Authorization': 'Bearer $accessToken',
        'Content-Type': 'application/json',
      };
}
