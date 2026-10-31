import { defineConfig } from "vitepress";

export default defineConfig({
  title: "Morph",
  description:
    "Entity-driven Backend-as-a-Service — auth, collections, access, files, GraphQL, processes, functions, and plugins.",
  lang: "en-US",
  cleanUrls: true,
  lastUpdated: true,
  ignoreDeadLinks: true,
  // Avoid clashing with the Dev Panel Vite server on 5173.
  vite: {
    server: { port: 5180, strictPort: true, host: "127.0.0.1" },
    preview: { port: 5180, host: "127.0.0.1" },
  },

  head: [
    ["link", { rel: "icon", href: "/favicon.svg" }],
    ["meta", { name: "theme-color", content: "#0c1f1a" }],
  ],

  themeConfig: {
    logo: { light: "/logo.svg", dark: "/logo.svg" },
    siteTitle: "Morph",
    outline: { level: [2, 3] },
    search: { provider: "local" },

    nav: [
      { text: "Start now", link: "/guide/quickstart" },
      { text: "Guides", link: "/guide/concepts" },
      { text: "API", link: "/api/" },
      { text: "SDKs", link: "/sdk/" },
      { text: "Plugins", link: "/plugins/" },
      {
        text: "More",
        items: [
          { text: "Architecture", link: "/architecture/" },
          { text: "Dev Panel", link: "/guide/dev-panel" },
          { text: "CLI", link: "/guide/cli" },
          { text: "Testing & validity", link: "/reference/testing" },
          { text: "Parity", link: "/reference/parity" },
        ],
      },
    ],

    sidebar: {
      "/guide/": [
        {
          text: "Get started",
          items: [
            { text: "Introduction", link: "/guide/introduction" },
            { text: "Quickstart", link: "/guide/quickstart" },
            { text: "Dev Panel", link: "/guide/dev-panel" },
            { text: "Your first app", link: "/guide/first-app" },
            { text: "Concepts", link: "/guide/concepts" },
          ],
        },
        {
          text: "Build with Morph",
          items: [
            { text: "Authentication", link: "/guide/auth" },
            { text: "Tenancy", link: "/guide/tenancy" },
            { text: "Collections & schema", link: "/guide/collections" },
            { text: "Records (Data API)", link: "/guide/records" },
            { text: "Access control", link: "/guide/access" },
            { text: "Files & storage", link: "/guide/files" },
            { text: "GraphQL", link: "/guide/graphql" },
            { text: "Governed SQL", link: "/guide/sql" },
            { text: "Processes", link: "/guide/processes" },
            { text: "Functions", link: "/guide/functions" },
            { text: "Webhooks", link: "/guide/webhooks" },
            { text: "SMS & email", link: "/guide/messaging" },
            { text: "Realtime", link: "/guide/realtime" },
            { text: "Credentials", link: "/guide/credentials" },
            { text: "Code-first & CLI", link: "/guide/cli" },
          ],
        },
      ],
      "/api/": [
        {
          text: "HTTP API",
          items: [
            { text: "Overview", link: "/api/" },
            { text: "Auth & OpenIddict", link: "/api/auth" },
            { text: "Platform", link: "/api/platform" },
            { text: "Data plane", link: "/api/data" },
            { text: "Files", link: "/api/files" },
            { text: "GraphQL & SQL", link: "/api/query" },
            { text: "Processes & functions", link: "/api/automation" },
            { text: "Plugins & health", link: "/api/ops" },
            { text: "Errors & headers", link: "/api/conventions" },
          ],
        },
      ],
      "/sdk/": [
        {
          text: "SDKs",
          items: [
            { text: "Overview", link: "/sdk/" },
            { text: "TypeScript (@morph/sdk)", link: "/sdk/typescript" },
            { text: ".NET (Morph.Client)", link: "/sdk/dotnet" },
          ],
        },
      ],
      "/plugins/": [
        {
          text: "Plugin development",
          items: [
            { text: "Overview", link: "/plugins/" },
            { text: "Quickstart addon", link: "/plugins/quickstart" },
            { text: "Manifest & permissions", link: "/plugins/manifest" },
            { text: "Builder APIs", link: "/plugins/builder" },
            { text: "Process activities", link: "/plugins/activities" },
            { text: "SMS providers", link: "/plugins/sms" },
            { text: "Inbound HTTP & webhooks", link: "/plugins/http" },
            { text: "Outbound HTTP (SSRF)", link: "/plugins/outbound" },
            { text: "Install & load", link: "/plugins/install" },
            { text: "Reference addons", link: "/plugins/reference-addons" },
            { text: "Security model", link: "/plugins/security" },
          ],
        },
      ],
      "/architecture/": [
        {
          text: "Technical docs",
          items: [
            { text: "Overview", link: "/architecture/" },
            { text: "System design", link: "/architecture/system" },
            { text: "Entity engine", link: "/architecture/entity-engine" },
            { text: "Access engine", link: "/architecture/access" },
            { text: "Process engine", link: "/architecture/process-engine" },
            { text: "Storage & crypto", link: "/architecture/storage-crypto" },
            { text: "Hosting & config", link: "/architecture/hosting" },
            { text: "Multi-region notes", link: "/architecture/multi-region" },
          ],
        },
      ],
      "/reference/": [
        {
          text: "Reference",
          items: [
            { text: "Glossary", link: "/reference/glossary" },
            { text: "Testing & validity", link: "/reference/testing" },
            { text: "Parity matrix", link: "/reference/parity" },
            { text: "ADRs", link: "/reference/adrs" },
            { text: "Changelog posture", link: "/reference/versioning" },
          ],
        },
      ],
    },

    socialLinks: [
      { icon: "github", link: "https://github.com/ParhamMoAsghari/morph" },
    ],

    editLink: {
      pattern: "https://github.com/ParhamMoAsghari/morph/edit/main/apps/docs/:path",
      text: "Edit this page on GitHub",
    },

    footer: {
      message: 'Released under the <a href="https://github.com/ParhamMoAsghari/morph/blob/main/LICENSE">Apache-2.0 License</a>',
      copyright: "Copyright © 2026 Morph contributors",
    },
  },
});
