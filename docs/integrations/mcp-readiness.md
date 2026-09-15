# Local MCP Readiness

**Checked**: 2026-09-15, Europe/Berlin
**Scope**: Codex desktop/CLI on this workstation

Installing an editor package, application, or add-on does not make its MCP tools available to an
already running Codex session. The integration is ready only when the editor-side bridge or remote
service is running, Codex has an enabled MCP server entry, the client has been restarted after a
configuration change, and the server's tools appear in the session tool inventory.

## Current state

| Integration | Installed/configured evidence | Live evidence | Verdict |
|---|---|---|---|
| Unity MCP | `com.coplaydev.unity-mcp` is pinned in `Packages/manifest.json`; Codex has enabled `unityMCP` at `http://127.0.0.1:8080/mcp` | The endpoint completed an MCP `2025-06-18` handshake with `mcp-for-unity-server` `3.4.7`, reported 48 tools and one connected `idle-factory-game@6c674e1399dbbaa5` editor on Unity `6000.6.0f1`, and a read-only `read_console` tool call succeeded with zero error/warning entries. This already-running Codex session still exposes no native `mcp__unityMCP__*` tool binding. | **SERVER READY / SESSION REFRESH REQUIRED**: Unity and its HTTP bridge are healthy; restart or refresh Codex to load the native tool bindings |
| ComfyUI local MCP | Comfy Desktop `1.0.47` is installed; Codex has enabled stdio server `comfy-mcp` with command `comfy-mcp` | The command is absent from `PATH`, the Python package is absent, and `uv tool list` contains only `specify-cli`; no ComfyUI process or Comfy tool is exposed | **BLOCKED**: the configured stdio executable is not installed or not discoverable |
| Comfy Cloud MCP | Codex has enabled `comfy-cloud` at `https://cloud.comfy.org/mcp` | The endpoint is reachable but returns HTTP `401` and requests a bearer token/OAuth scope `comfy-mcp:tools:call`; no Comfy tool is exposed | **BLOCKED**: authentication and a Codex refresh are required |
| Blender MCP | Blender `5.2.1` and the official Blender Lab MCP add-on `1.0.3` are installed; the add-on bridge defaults to `localhost:9876` | No Blender MCP entry exists in Codex, Blender is not running, and the separate client-side MCP server is not installed/configured | **BLOCKED**: add the separate MCP server, register it with Codex, run the Blender bridge, then refresh Codex |

The current session exposes no native tool whose name or description contains `unity`, `comfy`, or
`blender`. For Unity, a direct MCP handshake, instance-resource read, tool listing, and read-only
console call now prove that the configured server and editor connection are healthy; only this
session's native tool registration is stale. `codex mcp list` alone proves configuration only.

## Recovery and verification

### Unity

1. Open the project in the pinned Unity editor.
2. Open **Window > MCP for Unity**.
3. Run **Auto-Setup**, select Codex and the intended transport, and confirm the generated endpoint.
4. For local HTTP mode, start both **Unity Bridge** and **Local HTTP Server**. Confirm that
   `http://127.0.0.1:8080/mcp` is listening. For stdio mode, let Auto-Setup replace the HTTP entry
   with the package's generated `uvx ... mcp-for-unity --transport stdio` configuration.
5. Restart the Codex desktop app or IDE extension, open a new session, and verify the server through
   `/mcp` and the actual Unity tool inventory.

Until that succeeds, use the pinned Unity CLI wrapper documented in
`docs/testing/unity-test-harness.md`. It is the supported fallback for imports and Edit/Play tests.

### ComfyUI

Choose and document one provider before an asset-production assignment:

- **Cloud**: authenticate the existing `comfy-cloud` entry with `codex mcp login comfy-cloud`, then
  restart Codex and verify a real tool call.
- **Local**: install the intended, reviewed ComfyUI MCP server distribution and configure Codex with
  its absolute executable path or a reproducible package command. Starting Comfy Desktop alone does
  not supply the currently configured `comfy-mcp` executable.

Record the selected workflow/model version, cost boundary, license/provenance rules, and output path
in the asset brief before generation. A Comfy Cloud brief must also state which prompts and source
assets may leave the workstation; authentication credentials remain in Codex-managed storage and
must never be written to repository files.

### Blender

The Blender Lab installation consists of two parts: the Blender add-on bridge and a separate MCP
server launched by the client. The add-on is present, but the second part is missing.

1. Install the matching server from the official Blender Lab MCP release or reviewed source setup.
2. Register that server in Codex as a stdio server using its documented command. Do not register the
   raw `localhost:9876` socket as an HTTP MCP URL; it is the add-on bridge protocol.
3. Start Blender, enable online access if the add-on requests it, and enable **Auto Start** or click
   **Start MCP Bridge Server**. The installed add-on defaults to `localhost:9876`.
4. Restart Codex, verify the server with `/mcp`, and make a read-only scene-inspection call before an
   asset-production task is dispatched.

Blender MCP can execute generated Python inside Blender. Use it only in the isolated 3D/render
workflow and project paths defined by the assignment contract. Keep local Unity and Blender bridges
bound to loopback and stop them outside assigned sessions.

## Assignment gate

The parent agent checks the live tool inventory immediately before dispatch. A role may claim Unity,
ComfyUI, or Blender MCP use only after a successful tool call in that session. If a task explicitly
requires ComfyUI or Blender, leave that task blocked until the chosen server is connected; do not
silently substitute another generator. Unity development may use the documented CLI fallback when
the assignment permits it.

References:

- [OpenAI Codex MCP configuration](https://developers.openai.com/codex/mcp)
- [Blender Lab MCP Server](https://www.blender.org/lab/mcp-server/)
