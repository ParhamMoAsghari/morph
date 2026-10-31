/**
 * Minimal code-first app — no admin console.
 *
 * Prerequisites:
 *   morph login / link / push / publish / migrate --apply
 *   morph generate --lang ts
 */
import { MorphClient } from "@morph/sdk";

const baseUrl = process.env.MORPH_API_URL ?? "http://localhost:5100";
const environmentId = process.env.MORPH_ENV_ID;
const email = process.env.MORPH_EMAIL ?? "dev@morph.local";
const password = process.env.MORPH_PASSWORD ?? "Password1!";

if (!environmentId) {
  console.error("Set MORPH_ENV_ID to your linked environment GUID.");
  process.exit(1);
}

const morph = new MorphClient({ baseUrl, environmentId });
await morph.login(email, password);

const created = await morph.createRecord("employee", {
  employeeNumber: `E-${Date.now()}`,
  "person.fullName": "Ada Lovelace",
  "person.email": `ada-${Date.now()}@example.com`,
  "employment.title": "Engineer",
});

console.log("created", created);
const page = await morph.listRecords("employee");
console.log("listed", page.items?.length ?? 0, "row(s)");
