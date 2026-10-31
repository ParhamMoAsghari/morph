import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { fileURLToPath, URL } from "node:url";

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@morph/sdk": fileURLToPath(
        new URL("../../sdks/typescript/src/index.ts", import.meta.url),
      ),
    },
  },
  server: {
    host: "127.0.0.1",
    port: 5173,
    strictPort: true,
    proxy: {
      "/v1": "http://localhost:5100",
      "/data": "http://localhost:5100",
      "/files": "http://localhost:5100",
      "/connect": "http://localhost:5100",
      "/graphql": "http://localhost:5100",
      "/sql": "http://localhost:5100",
      "/plugins": "http://localhost:5100",
      "/health": "http://localhost:5100",
      "/realtime": {
        target: "http://localhost:5100",
        ws: true,
      },
    },
  },
});
