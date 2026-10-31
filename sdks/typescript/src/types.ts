export interface MorphClientOptions {
  baseUrl: string;
  accessToken?: string;
  apiKey?: string;
  environmentId?: string;
  projectId?: string;
  platformClientId?: string;
  platformClientSecret?: string;
  scope?: string;
  fetch?: typeof fetch;
}

export interface TokenResponse {
  access_token: string;
  token_type: string;
  expires_in: number;
  refresh_token?: string;
}

export interface RegisterResponse {
  id: string;
  email: string;
  displayName?: string | null;
}

export interface DefinitionSummary {
  definitionId: string;
  environmentId: string;
  slug: string;
  displayName: string;
  kind: string;
  currentPublishedVersionId?: string | null;
}

export interface FieldDto {
  fieldId?: string;
  slug: string;
  displayName: string;
  dataType: string;
  isRequired?: boolean;
  isIndexed?: boolean;
  isUnique?: boolean;
  encrypt?: boolean;
}

export interface CreateDefinitionRequest {
  slug: string;
  displayName: string;
  kind: string;
  fields?: FieldDto[];
}

export interface EntityRecord {
  id: string;
  entity?: string;
  definitionVersionId?: string;
  fields?: Record<string, unknown>;
  [key: string]: unknown;
}

export interface Page<T> {
  items: T[];
  nextCursor?: string | null;
  hasMore?: boolean;
}

export interface RoleSummary {
  roleId: string;
  name: string;
  slug: string;
}

export interface PolicySummary {
  policyId: string;
  name: string;
  status?: string;
}

export interface ProcessSummary {
  definitionId: string;
  slug: string;
  displayName?: string;
  currentPublishedVersionId?: string | null;
}

export interface FunctionSummary {
  slug: string;
  runtime?: string;
  status?: string;
}

export interface WebhookSummary {
  id: string;
  url: string;
  events?: string[];
  enabled?: boolean;
}

export interface CredentialSummary {
  id: string;
  name: string;
  prefix?: string;
  createdAt?: string;
  revokedAt?: string | null;
}

export interface PluginSummary {
  id: string;
  version?: string;
  displayName?: string;
  enabled?: boolean;
}

export interface BucketSummary {
  id?: string;
  slug: string;
  displayName?: string;
}
