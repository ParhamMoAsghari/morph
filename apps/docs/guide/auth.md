# Authentication

Morph uses **ASP.NET Identity** users plus **OpenIddict** for tokens.

## Register

```http
POST /v1/auth/register
Content-Type: application/json

{
  "email": "dev@morph.local",
  "password": "Password1!",
  "displayName": "Dev"
}
```

## Token (password grant)

Development seeds confidential client `morph-platform` / `morph-platform-dev-secret`.

```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password
&username=dev@morph.local
&password=Password1!
&client_id=morph-platform
&client_secret=morph-platform-dev-secret
&scope=openid profile email offline_access morph.platform
```

Use `Authorization: Bearer {access_token}` on subsequent calls.

## Me

```http
GET /v1/auth/me
```

Returns the current user id (needed for role bindings).

## Password reset & email verify

Forgot-password returns **Accepted** without leaking whether the email exists. Reset/verify with invalid tokens fail validation — covered by Host e2e.

## SDK

```ts
await morph.register("dev@morph.local", "Password1!", "Dev");
await morph.login("dev@morph.local", "Password1!");
await morph.me();
```

Configure `platformClientId` / `platformClientSecret` if you override Development defaults.
