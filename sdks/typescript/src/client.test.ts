import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { MorphClient, MorphApiError } from "./client.ts";

describe("MorphClient", () => {
  it("builds auth and env headers", async () => {
    const calls: { url: string; init?: RequestInit }[] = [];
    const client = new MorphClient({
      baseUrl: "http://morph.test",
      accessToken: "tok",
      environmentId: "11111111-1111-1111-1111-111111111111",
      fetch: async (url, init) => {
        calls.push({ url: String(url), init });
        return new Response(JSON.stringify({ items: [], hasMore: false }), {
          status: 200,
          headers: { "content-type": "application/json" },
        });
      },
    });

    await client.listRecords("employee");
    assert.equal(calls.length, 1);
    assert.equal(calls[0].url, "http://morph.test/data/v1/employee");
    const headers = calls[0].init?.headers as Record<string, string>;
    assert.equal(headers.authorization, "Bearer tok");
    assert.equal(headers["x-morph-env-id"], "11111111-1111-1111-1111-111111111111");
  });

  it("throws MorphApiError on failure", async () => {
    const client = new MorphClient({
      baseUrl: "http://morph.test",
      fetch: async () => new Response("nope", { status: 401 }),
    });
    await assert.rejects(() => client.me(), (err: unknown) => {
      assert.ok(err instanceof MorphApiError);
      assert.equal(err.status, 401);
      assert.equal((err as MorphApiError).body, "nope");
      return true;
    });
  });

  it("createRecord sends x-morph-env-id header", async () => {
    const calls: { url: string; init?: RequestInit }[] = [];
    const envId = "22222222-2222-2222-2222-222222222222";
    const client = new MorphClient({
      baseUrl: "http://morph.test",
      accessToken: "tok",
      environmentId: envId,
      fetch: async (url, init) => {
        calls.push({ url: String(url), init });
        return new Response(JSON.stringify({ id: "r1", fields: { title: "x" } }), {
          status: 200,
          headers: { "content-type": "application/json" },
        });
      },
    });

    await client.createRecord("task", { title: "x" });
    assert.equal(calls.length, 1);
    assert.equal(calls[0].url, "http://morph.test/data/v1/task");
    const headers = calls[0].init?.headers as Record<string, string>;
    assert.equal(headers["x-morph-env-id"], envId);
  });

  it("login posts form-urlencoded grant_type password", async () => {
    const calls: { url: string; init?: RequestInit }[] = [];
    const client = new MorphClient({
      baseUrl: "http://morph.test",
      fetch: async (url, init) => {
        calls.push({ url: String(url), init });
        return new Response(
          JSON.stringify({ access_token: "abc", token_type: "Bearer", expires_in: 3600 }),
          { status: 200, headers: { "content-type": "application/json" } },
        );
      },
    });

    await client.login("a@b.c", "secret");
    assert.equal(calls.length, 1);
    assert.equal(calls[0].url, "http://morph.test/connect/token");
    const headers = calls[0].init?.headers as Record<string, string>;
    assert.equal(headers["content-type"], "application/x-www-form-urlencoded");
    const body = calls[0].init?.body?.toString() ?? "";
    assert.match(body, /grant_type=password/);
    assert.match(body, /username=a%40b\.c/);
    assert.match(body, /password=secret/);
  });

  it("MorphApiError exposes response body", async () => {
    const client = new MorphClient({
      baseUrl: "http://morph.test",
      fetch: async () =>
        new Response('{"code":"forbidden"}', { status: 403 }),
    });
    await assert.rejects(() => client.createOrganization("Acme", "acme"), (err: unknown) => {
      assert.ok(err instanceof MorphApiError);
      assert.equal(err.status, 403);
      assert.equal(err.body, '{"code":"forbidden"}');
      return true;
    });
  });
});
