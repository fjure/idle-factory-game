# Local MCP Readiness

**Checked**: 2026-09-30, Europe/Berlin
**Scope**: Codex and Claude Code sessions on this workstation

Installing an editor package, application, or add-on does not make its MCP tools available to an
already running agent session. The integration is ready only when the editor-side bridge or remote
service is running, the intended client has an enabled MCP server entry, the client has restarted after a
configuration change, and the server's tools appear in the session tool inventory.

## Current state

| Integration | Installed/configured evidence | Live evidence | Verdict |
|---|---|---|---|
| Unity MCP | `com.coplaydev.unity-mcp` is pinned in `Packages/manifest.json`; `unityMCP` is configured at `http://127.0.0.1:8080/mcp` | The endpoint previously completed an MCP `2025-06-18` handshake with `mcp-for-unity-server` `3.4.7`, reported 48 tools and a connected Unity `6000.6.0f1` editor, and completed a read-only `read_console` call. The project owner reconfirmed on 2026-09-17 that the Unity MCP server is active. Tool bindings remain session-specific and must appear in the target agent's inventory before use. | **SERVER ACTIVE / VERIFY PER SESSION**: prefer focused Unity MCP calls whenever the target session exposes them; otherwise use the documented CLI fallback |
| ComfyUI local MCP | Comfy Desktop `1.0.47` is running. `comfy-mcp 0.10.0` and `comfy-cli 1.22.0` are installed in an isolated `uv` tool environment. Codex has a global MCP entry; Claude Code has a project entry. Both use absolute executable paths and the local endpoint `127.0.0.1:8188`. | The ComfyUI endpoint returned HTTP `200` and reported ComfyUI `0.38.0`. A real MCP stdio initialize, 39-tool listing, and read-only `server_info` call succeeded; it reported the running server and NVIDIA RTX 5070 Ti with no compatibility warnings. On 2026-09-30 the comfy-cli default workspace was pointed at the running Comfy Desktop install (see below); `server_info` then reported core `v0.38.0` (current) and a detected manager. With the user's approval, the Z-Image-Turbo model files (Apache-2.0) were installed; the `image_z_image_turbo` template then passed `local_check` (`runnable: true`) and produced a 1024×1024 smoke-test image through Claude Code's `run_workflow`. | **READY / VERIFY PER SESSION**: call `server_info` in each session before dispatching asset work |
| Comfy Cloud MCP | Codex still has `comfy-cloud` configured at `https://cloud.comfy.org/mcp`. | Not retested during this check; the 2026-09-17 check returned HTTP `401` and requested Comfy Cloud authentication. | **AUTH REQUIRED**: use the local provider above unless cloud generation is explicitly selected and authenticated |
| Blender MCP | Blender `5.2.1`, the official Blender Lab add-on `1.0.3`, and MCP server `1.0.2` (source commit `dbbf836ad4b1025f14a2b3b504c43903f39e0b04`) are installed. Codex has a global MCP entry; Claude Code has a project entry. Both launch the stdio server against loopback port `9876`. | With the user's approval, Blender Online Access was enabled, the existing Blender Lab extension repository was registered, and MCP add-on Auto Start was enabled for `127.0.0.1:9876`. The loopback port accepts connections. A real MCP initialize, 26-tool listing, and read-only `get_objects_summary` call succeeded against the live default scene. | **SERVER AND CONFIG READY / VERIFY PER SESSION**: the Blender bridge and both client configurations are ready; refresh each client session and make a tool call before dispatching Blender asset work |

The Unity plugin for Claude Code was installed on 2026-10-08. It adds `unity:*` skills to Claude Code
sessions only; Codex does not have them. They are knowledge, not tools, so they need no live
verification. They do not replace the Unity MCP server above. AGENTS.md
("Unity plugin skills (Claude Code only)") lists which skills apply to which role and the guardrails.

Server readiness and agent-session tool registration are separate facts. A direct Unity handshake,
instance-resource read, tool listing, and read-only console call prove that the configured server and
editor connection are healthy, while each Kilo/Codex session must still expose and successfully call
the native Unity tools before an assignment claims MCP evidence. A configured server list alone is
not sufficient. The local Comfy MCP server and Blender bridge were tested directly, but newly added
client entries do not appear in this already-running Codex session. Restart Codex or open a fresh
session and make a real tool call before claiming its tools are available there. Claude Code has the
same per-session refresh requirement.

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

When the tools are available, prefer narrow structured calls for console inspection, targeted test
status, scene/hierarchy state, serialized objects, and importer settings. These calls avoid broad log
or YAML ingestion and reduce token usage. Keep the CLI runner authoritative for complete reproducible
Edit Mode and Play Mode suites, and avoid asking MCP and CLI to produce duplicate full evidence unless
an independent gate requires it.

### ComfyUI

Choose and document one provider before an asset-production assignment:

- **Cloud**: authenticate the existing `comfy-cloud` entry with `codex mcp login comfy-cloud`, then
  restart Codex and verify a real tool call.
- **Local**: the official `Comfy-Org/comfy-mcp` package and `comfy-cli` are installed. Codex and
  Claude Code use the absolute `comfy-mcp.exe` path, `COMFY_BIN`, and
  `COMFY_LOCAL_URL=http://127.0.0.1:8188`. The CLI's local state is redirected to the ignored
  `.utmp/comfy-mcp-home/` directory because its default `%USERPROFILE%\AppData\Local\comfy-cli`
  location is not writable in the restricted agent shell. `server_info` succeeded against the live
  Comfy Desktop backend. Refresh the client session and repeat that call before assigning the tools.
- **Workspace binding**: comfy-cli does not discover Comfy Desktop installs by itself. Its
  `default_workspace` in `.utmp/comfy-mcp-home/AppData/Local/comfy-cli/config.ini` must point at
  the `ComfyUI` folder of the install Comfy Desktop actually launches, currently
  `%LOCALAPPDATA%\Comfy-Desktop\ComfyUI-Installs\ComfyUI (1)\ComfyUI` (the install named
  "ComfyUI (1)" in Comfy Desktop's `installations.json`). If Desktop launches a different install, or
  the ignored `.utmp/` is cleaned, re-run
  `comfy --skip-prompt set-default "<install>\ComfyUI"` with the same `HOME`/`USERPROFILE`
  redirect and confirm `server_info.workspace.path` and `freshness.core.installed`.
- **Models**: Comfy Desktop's shared model root is
  `%LOCALAPPDATA%\Comfy-Desktop\ComfyUI-Shared\models` (wired via its generated
  `extra_model_paths` file). `download_model` writes relative to the comfy-cli workspace instead, so
  the installed Z-Image-Turbo files live in the "ComfyUI (1)" install's own `models/` folder:
  `diffusion_models/z_image_turbo_bf16.safetensors`, `text_encoders/qwen_3_4b.safetensors`, and
  `vae/ae.safetensors`, all from `huggingface.co/Comfy-Org/z_image_turbo` (model license
  Apache-2.0 per the `Tongyi-MAI/Z-Image-Turbo` model card). Move them to the shared root if
  Desktop switches installs. Every asset brief still records the model, license, and workflow, and
  the template's `local_check` must report `runnable: true` before generation.

Record the selected workflow/model version, cost boundary, license/provenance rules, and output path
in the asset brief before generation. A Comfy Cloud brief must also state which prompts and source
assets may leave the workstation; authentication credentials remain in Codex-managed storage and
must never be written to repository files.

### Blender

The official Blender Lab integration has two parts: the Blender extension bridge and a separate
stdio MCP server launched by each client. Both components are installed and configured for Codex
and Claude Code. On 2026-09-30, with the user's approval, Blender Online Access was enabled, the
existing local Blender Lab repository was registered in Blender preferences, and the official MCP
extension was enabled with Auto Start at `127.0.0.1:9876`. The bridge is running and passed a
read-only scene call through Blender MCP.

1. Keep Blender running with the **MCP** extension enabled and **Auto Start** configured. The
   bridge binds to loopback port `9876`; Blender Online Access is enabled in saved preferences.
2. Restart Codex and Claude Code (or open fresh sessions). The Codex server uses the installed
   `blender-mcp.exe`; Claude Code has the equivalent project MCP entry. Do not register the raw
   `localhost:9876` socket as an HTTP MCP URL; it is the add-on bridge protocol.
3. Verify the extension listener, then make a read-only scene inspection call before dispatching an
   asset-production task. Leave that task blocked until the scene call succeeds.

Blender MCP can execute generated Python inside Blender. Use it only in the isolated 3D/render
workflow and project paths defined by the assignment contract. Keep local Unity and Blender bridges
bound to loopback and stop them outside assigned sessions.

## Art tooling pins (Feature 004, T403)

Batch art production uses the headless CLI, not the MCP bridges (research R07, R14). Preflight with
`scripts/check-art-tooling.ps1`: it reports `OK`, `FAIL` (wrong version) or `tooling unavailable`
(missing or unreachable; blocks only art-production Issues) and exits 0, 1 or 2 respectively.

| Item | Pin / default | Checked |
|---|---|---|
| `BLENDER_EXE` default | `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe` (the variable is not set on this workstation; the scripts fall back to this path) | 2026-10-27, Blender `5.2.1 LTS` (build 2026-08-25, hash `9e2066aef7ef`) |
| `uv` | `0.8.22` at `%USERPROFILE%\.local\bin\uv.exe` | 2026-10-27 |
| Rig | `tools/art-pipeline/blender/rig-v2.blend` (sha256 `9075ca24da793f8b2d04e8d4642aefc139cfad3329dc1085528b2947dc412098`; `rig-v1.blend` rendered 2.236:1 and is kept for provenance only), regenerated by `build_rig.py`; Cycles CPU, seed 0, 16 samples, no denoiser | 2026-10-27 |

Rig commands (run from `tools/art-pipeline/blender/`):

- Rebuild: `"$BLENDER_EXE" -b --factory-startup --python-exit-code 1 -P build_rig.py`
- Read back and verify the saved rig: `"$BLENDER_EXE" -b rig-v2.blend --factory-startup --python-exit-code 1 -P inspect_rig.py`
- Render: `"$BLENDER_EXE" -b rig-v2.blend --factory-startup --python-exit-code 1 -P render.py -- <asset> [--projection oblique-3-4|isometric-2-1] [--material flat|toon] [--out <dir>]`; writes the PNG and a provenance stub (Blender version, GPU, driver, render time, hashes). `<asset>` is `cube` or a `.blend`/`.glb`/`.obj`/`.stl` path.
- Reproducibility smoke (R09, tolerance 0.5 % of opaque pixels): `python smoke_test.py` (standard library only; it renders the test cube twice and compares).

Warning: Cycles clamps `filter_width` to its 0.01 minimum (it cannot be 0, so the filter is effectively but not exactly off); `render.filter_size` is 0. The rig also disables every metadata stamp, so PNGs carry no Blender text chunks (no local path, date or render time) and byte hashes are reproducible. The batch path renders on the CPU. It holds the `gpu` lease to record GPU and driver and to keep GPU load from other sessions off the run.

Session check 2026-10-27 (T403 worker): `comfy-mcp server_info` answered but reported the ComfyUI server not running (`http://127.0.0.1:8188` unreachable); the Blender MCP bridge refused connections on `127.0.0.1:9876` (Blender not running). Neither blocks the headless batch route.

## Assignment gate

The parent agent checks the live tool inventory immediately before dispatch. A role may claim Unity,
ComfyUI, or Blender MCP use only after a successful tool call in that session. If a task explicitly
requires ComfyUI or Blender, leave that task blocked until the chosen server is connected; do not
silently substitute another generator. Unity development may use the documented CLI fallback when
the assignment permits it.

References:

- [OpenAI Codex MCP configuration](https://developers.openai.com/codex/mcp)
- [Official Comfy-Org local MCP server](https://github.com/Comfy-Org/comfy-mcp)
- [Blender Lab MCP Server](https://www.blender.org/lab/mcp-server/)
