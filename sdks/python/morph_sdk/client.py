"""Thin Morph Python client stub. Full OpenAPI codegen comes later."""

from __future__ import annotations

from typing import Any, Optional

import urllib.error
import urllib.request
import json


class MorphClient:
    def __init__(self, base_url: str, access_token: Optional[str] = None) -> None:
        self.base_url = base_url.rstrip("/")
        self.access_token = access_token

    def list_records(self, environment_id: str, entity: str) -> Any:
        return self._request("GET", f"/data/v1/{environment_id}/{entity}")

    def run_process(self, environment_id: str, slug: str, body: Optional[dict] = None) -> Any:
        return self._request(
            "POST",
            f"/v1/environments/{environment_id}/processes/{slug}/run",
            body or {},
        )

    def invoke_function(self, environment_id: str, slug: str, body: Optional[dict] = None) -> Any:
        return self._request(
            "POST",
            f"/v1/environments/{environment_id}/functions/{slug}/invoke",
            body or {},
        )

    def _request(self, method: str, path: str, body: Optional[dict] = None) -> Any:
        url = f"{self.base_url}{path}"
        data = None if body is None else json.dumps(body).encode("utf-8")
        req = urllib.request.Request(url, data=data, method=method)
        req.add_header("Content-Type", "application/json")
        if self.access_token:
            req.add_header("Authorization", f"Bearer {self.access_token}")
        try:
            with urllib.request.urlopen(req) as resp:
                raw = resp.read().decode("utf-8")
                return json.loads(raw) if raw else None
        except urllib.error.HTTPError as exc:
            raise RuntimeError(f"Morph HTTP {exc.code}: {exc.read().decode('utf-8', errors='replace')}") from exc
