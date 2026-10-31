import type {
  BucketSummary,
  CreateDefinitionRequest,
  CredentialSummary,
  DefinitionSummary,
  EntityRecord,
  FunctionSummary,
  MorphClientOptions,
  Page,
  PluginSummary,
  PolicySummary,
  ProcessSummary,
  RegisterResponse,
  RoleSummary,
  TokenResponse,
  WebhookSummary,
} from "./types.js";

export class MorphApiError extends Error {
  readonly status: number;
  readonly body: string;

  constructor(status: number, body: string) {
    super(`Morph API ${status}: ${body.slice(0, 400)}`);
    this.name = "MorphApiError";
    this.status = status;
    this.body = body;
  }
}

export class MorphClient {
  private readonly baseUrl: string;
  private accessToken?: string;
  private readonly apiKey?: string;
  private environmentId?: string;
  private projectId?: string;
  private readonly platformClientId: string;
  private readonly platformClientSecret: string;
  private readonly scope: string;
  private readonly fetchImpl: typeof fetch;

  constructor(options: MorphClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, "");
    this.accessToken = options.accessToken;
    this.apiKey = options.apiKey;
    this.environmentId = options.environmentId;
    this.projectId = options.projectId;
    this.platformClientId = options.platformClientId ?? "morph-platform";
    this.platformClientSecret =
      options.platformClientSecret ?? "morph-platform-dev-secret";
    this.scope =
      options.scope ?? "openid profile email offline_access morph.platform";
    // Native window.fetch must keep its receiver; a bare `fetch` ref throws Illegal invocation.
    this.fetchImpl =
      options.fetch ?? ((input, init) => globalThis.fetch(input, init));
  }

  getBaseUrl(): string {
    return this.baseUrl;
  }

  getEnvironmentId(): string | undefined {
    return this.environmentId;
  }

  getProjectId(): string | undefined {
    return this.projectId;
  }

  getAccessToken(): string | undefined {
    return this.accessToken;
  }

  setAccessToken(token: string): void {
    this.accessToken = token;
  }

  setEnvironmentId(environmentId: string): void {
    this.environmentId = environmentId;
  }

  setProjectId(projectId: string): void {
    this.projectId = projectId;
  }

  async register(email: string, password: string, displayName?: string): Promise<RegisterResponse> {
    return this.request<RegisterResponse>("POST", "/v1/auth/register", {
      body: { email, password, displayName },
      auth: false,
    });
  }

  async login(email: string, password: string): Promise<TokenResponse> {
    const body = new URLSearchParams({
      grant_type: "password",
      username: email,
      password,
      client_id: this.platformClientId,
      client_secret: this.platformClientSecret,
      scope: this.scope,
    });

    const res = await this.fetchImpl(`${this.baseUrl}/connect/token`, {
      method: "POST",
      headers: { "content-type": "application/x-www-form-urlencoded" },
      body,
    });
    const text = await res.text();
    if (!res.ok) {
      throw new MorphApiError(res.status, text);
    }
    const token = JSON.parse(text) as TokenResponse;
    this.accessToken = token.access_token;
    return token;
  }

  async me(): Promise<unknown> {
    return this.request("GET", "/v1/auth/me");
  }

  async createOrganization(name: string, slug: string): Promise<unknown> {
    return this.request("POST", "/v1/orgs", { body: { name, slug } });
  }

  async createProject(organizationId: string, name: string, slug: string): Promise<unknown> {
    return this.request("POST", "/v1/projects", {
      body: { organizationId, name, slug },
    });
  }

  async createEnvironment(projectId: string, name: string, slug: string): Promise<unknown> {
    return this.request("POST", `/v1/projects/${projectId}/environments`, {
      body: { name, slug },
    });
  }

  async listDefinitions(environmentId?: string): Promise<DefinitionSummary[]> {
    const env = environmentId ?? this.requireEnv();
    return this.request("GET", `/v1/environments/${env}/definitions`);
  }

  async createDefinition(
    request: CreateDefinitionRequest,
    environmentId?: string,
  ): Promise<unknown> {
    const env = environmentId ?? this.requireEnv();
    return this.request("POST", `/v1/environments/${env}/definitions`, {
      body: request,
    });
  }

  async publishDefinition(versionId: string, environmentId?: string): Promise<unknown> {
    const env = environmentId ?? this.requireEnv();
    return this.request(
      "POST",
      `/v1/environments/${env}/definitions/versions/${versionId}/publish`,
    );
  }

  async getEffectiveModel(versionId: string, environmentId?: string): Promise<unknown> {
    const env = environmentId ?? this.requireEnv();
    return this.request(
      "GET",
      `/v1/environments/${env}/definitions/versions/${versionId}/effective-model`,
    );
  }

  async planMigration(toVersionId: string, environmentId?: string): Promise<unknown> {
    const env = environmentId ?? this.requireEnv();
    return this.request("POST", `/v1/environments/${env}/migrations/plan`, {
      body: { toVersionId },
    });
  }

  async applyMigration(toVersionId: string, environmentId?: string): Promise<unknown> {
    const env = environmentId ?? this.requireEnv();
    return this.request("POST", `/v1/environments/${env}/migrations/apply`, {
      body: { toVersionId },
    });
  }

  async bindRole(
    principalId: string,
    roleName: string,
    environmentId?: string,
    projectId?: string,
  ): Promise<unknown> {
    const project = projectId ?? this.requireProject();
    return this.request("POST", `/v1/projects/${project}/role-bindings`, {
      body: {
        principalId,
        roleName,
        environmentId: environmentId ?? this.environmentId,
      },
    });
  }

  async exportDefinitions(environmentId?: string): Promise<unknown> {
    const env = environmentId ?? this.requireEnv();
    return this.request("GET", `/v1/environments/${env}/backup/definitions`);
  }

  async listRecords(entity: string, query?: Record<string, string>): Promise<Page<EntityRecord>> {
    const qs = query ? `?${new URLSearchParams(query).toString()}` : "";
    return this.request("GET", `/data/v1/${encodeURIComponent(entity)}${qs}`, {
      env: true,
    });
  }

  async createRecord(entity: string, fields: Record<string, unknown>): Promise<EntityRecord> {
    return this.request("POST", `/data/v1/${encodeURIComponent(entity)}`, {
      body: fields,
      env: true,
    });
  }

  async getRecord(entity: string, id: string): Promise<EntityRecord> {
    return this.request("GET", `/data/v1/${encodeURIComponent(entity)}/${id}`, {
      env: true,
    });
  }

  async updateRecord(
    entity: string,
    id: string,
    fields: Record<string, unknown>,
  ): Promise<EntityRecord> {
    return this.request("PATCH", `/data/v1/${encodeURIComponent(entity)}/${id}`, {
      body: fields,
      env: true,
    });
  }

  async deleteRecord(entity: string, id: string): Promise<void> {
    await this.request("DELETE", `/data/v1/${encodeURIComponent(entity)}/${id}`, {
      env: true,
    });
  }

  async graphql(query: string, variables?: Record<string, unknown>): Promise<unknown> {
    return this.request("POST", "/graphql", {
      body: { query, variables },
      env: true,
    });
  }

  async sql(statement: string): Promise<unknown> {
    return this.request("POST", "/sql/v1/query", {
      body: { sql: statement },
      env: true,
    });
  }

  async createBucket(slug: string, displayName?: string): Promise<BucketSummary> {
    return this.request("POST", "/files/v1/buckets", {
      body: { slug, displayName },
      env: true,
    });
  }

  async listRoles(projectId?: string): Promise<RoleSummary[]> {
    const project = projectId ?? this.requireProject();
    const roles = await this.request<Array<RoleSummary & { displayName?: string }>>(
      "GET",
      `/v1/projects/${project}/roles`,
    );
    return (Array.isArray(roles) ? roles : []).map((r) => ({
      ...r,
      slug: r.slug ?? r.name,
    }));
  }

  async createRole(
    name: string,
    displayName?: string,
    projectId?: string,
    inherits?: string[],
  ): Promise<RoleSummary> {
    const project = projectId ?? this.requireProject();
    const role = await this.request<RoleSummary & { displayName?: string }>(
      "POST",
      `/v1/projects/${project}/roles`,
      {
        body: {
          name,
          displayName: displayName ?? name,
          inherits: inherits ?? ["viewer"],
        },
      },
    );
    return { ...role, slug: role.slug ?? role.name };
  }

  async listPolicies(projectId?: string): Promise<PolicySummary[]> {
    const project = projectId ?? this.requireProject();
    return this.request("GET", `/v1/projects/${project}/policies`);
  }

  async listCredentials(projectId?: string): Promise<CredentialSummary[]> {
    const project = projectId ?? this.requireProject();
    return this.request("GET", `/v1/projects/${project}/credentials`);
  }

  async issueCredential(name: string, projectId?: string): Promise<unknown> {
    const project = projectId ?? this.requireProject();
    return this.request("POST", `/v1/projects/${project}/credentials`, {
      body: { name },
    });
  }

  async revokeCredential(credentialId: string, projectId?: string): Promise<unknown> {
    const project = projectId ?? this.requireProject();
    return this.request("POST", `/v1/projects/${project}/credentials/${credentialId}/revoke`);
  }

  async listWebhooks(projectId?: string): Promise<WebhookSummary[]> {
    const project = projectId ?? this.requireProject();
    const rows = await this.request<
      Array<{
        id: string;
        targetUrl?: string;
        url?: string;
        eventTypes?: string[];
        events?: string[];
        isEnabled?: boolean;
        enabled?: boolean;
      }>
    >("GET", `/v1/projects/${project}/webhooks`);
    return (Array.isArray(rows) ? rows : []).map((h) => ({
      id: h.id,
      url: h.url ?? h.targetUrl ?? "",
      events: h.events ?? h.eventTypes ?? [],
      enabled: h.enabled ?? h.isEnabled ?? true,
    }));
  }

  async createWebhook(
    url: string,
    events: string[],
    projectId?: string,
  ): Promise<WebhookSummary> {
    const project = projectId ?? this.requireProject();
    const created = await this.request<{
      id: string;
      targetUrl?: string;
      url?: string;
      eventTypes?: string[];
      events?: string[];
      isEnabled?: boolean;
      enabled?: boolean;
    }>("POST", `/v1/projects/${project}/webhooks`, {
      body: { targetUrl: url, eventTypes: events },
    });
    return {
      id: created.id,
      url: created.url ?? created.targetUrl ?? url,
      events: created.events ?? created.eventTypes ?? events,
      enabled: created.enabled ?? created.isEnabled ?? true,
    };
  }

  async listProcesses(environmentId?: string): Promise<ProcessSummary[]> {
    const env = environmentId ?? this.requireEnv();
    return this.request("GET", `/v1/environments/${env}/processes`);
  }

  async runProcess(
    slug: string,
    input?: Record<string, unknown>,
    environmentId?: string,
  ): Promise<unknown> {
    const env = environmentId ?? this.requireEnv();
    return this.request("POST", `/v1/environments/${env}/processes/${slug}/run`, {
      body: { input: input ?? {} },
    });
  }

  async getProcessLogs(instanceId: string, environmentId?: string): Promise<unknown> {
    const env = environmentId ?? this.requireEnv();
    return this.request(
      "GET",
      `/v1/environments/${env}/processes/instances/${instanceId}/logs`,
    );
  }

  async listFunctions(environmentId?: string): Promise<FunctionSummary[]> {
    const env = environmentId ?? this.requireEnv();
    return this.request("GET", `/v1/environments/${env}/functions`);
  }

  async registerFunction(
    slug: string,
    runtime: string,
    source?: string,
    environmentId?: string,
  ): Promise<unknown> {
    const env = environmentId ?? this.requireEnv();
    return this.request("POST", `/v1/environments/${env}/functions`, {
      body: { slug, runtime, source },
    });
  }

  async invokeFunction(
    slug: string,
    payload?: Record<string, unknown>,
    environmentId?: string,
  ): Promise<unknown> {
    const env = environmentId ?? this.requireEnv();
    return this.request("POST", `/v1/environments/${env}/functions/${slug}/invoke`, {
      body: { input: payload ?? {} },
    });
  }

  async listPlugins(): Promise<PluginSummary[]> {
    const payload = await this.request<PluginSummary[] | { plugins?: PluginSummary[] }>(
      "GET",
      "/plugins/v1",
    );
    if (Array.isArray(payload)) {
      return payload;
    }
    return Array.isArray(payload?.plugins) ? payload.plugins : [];
  }

  async sendSms(to: string, message: string): Promise<unknown> {
    return this.request("POST", "/v1/sms/send", {
      body: { to, body: message, message },
    });
  }

  async healthReady(): Promise<unknown> {
    return this.request("GET", "/health/ready", { auth: false });
  }

  private requireEnv(): string {
    if (!this.environmentId) {
      throw new Error("environmentId is required");
    }
    return this.environmentId;
  }

  private requireProject(): string {
    if (!this.projectId) {
      throw new Error("projectId is required");
    }
    return this.projectId;
  }

  private async request<T = unknown>(
    method: string,
    path: string,
    options?: { body?: unknown; auth?: boolean; env?: boolean },
  ): Promise<T> {
    const headers: Record<string, string> = {};
    const auth = options?.auth !== false;
    const token = this.accessToken ?? this.apiKey;
    if (auth && token) {
      headers.authorization = `Bearer ${token}`;
    }
    if (options?.env && this.environmentId) {
      headers["x-morph-env-id"] = this.environmentId;
    }
    if (this.projectId) {
      headers["x-morph-project-id"] = this.projectId;
    }
    let body: string | undefined;
    if (options?.body !== undefined) {
      headers["content-type"] = "application/json";
      body = JSON.stringify(options.body);
    }

    const res = await this.fetchImpl(`${this.baseUrl}${path}`, {
      method,
      headers,
      body,
    });
    const text = await res.text();
    if (!res.ok) {
      throw new MorphApiError(res.status, text);
    }
    if (!text) {
      return undefined as T;
    }
    return JSON.parse(text) as T;
  }
}
