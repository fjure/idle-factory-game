[CmdletBinding()]
param(
    # Snippet from profiler-snippets/ (without .cs.txt), or 'role' to print the answering process and its role.
    [Parameter(Mandatory)] [string] $Snippet,
    [string] $Out = '',
    [string] $Endpoint = 'http://127.0.0.1:8080/mcp'
)

# T064: minimal MCP (streamable HTTP) client for MCP for Unity. It sends a profiler snippet to execute_code and
# refuses to act unless the answering Unity process is the Profiler (Standalone Process), role "profiler".
# Precondition: the main editor's MCP bridge is stopped, so only the profiler process is registered.

$ErrorActionPreference = 'Stop'

function Invoke-Mcp([string] $Body, [string] $SessionId) {
    $headers = @{ Accept = 'application/json, text/event-stream' }
    if ($SessionId) { $headers['mcp-session-id'] = $SessionId }
    Invoke-WebRequest -UseBasicParsing -Uri $Endpoint -Method Post -Headers $headers -ContentType 'application/json' -Body $Body -TimeoutSec 300
}

function Read-Result($Response) {
    $content = $Response.Content
    if ($content -is [byte[]]) { $content = [Text.Encoding]::UTF8.GetString($content) }
    $json = ($content -split "`n" | Where-Object { $_ -like 'data:*' } | ForEach-Object { $_.Substring(5) } | Select-Object -Last 1)
    if (-not $json) { $json = $content }
    ($json | ConvertFrom-Json).result.structuredContent
}

function Invoke-Code([string] $Code, [string] $SessionId) {
    $body = @{ jsonrpc = '2.0'; id = 2; method = 'tools/call'; params = @{ name = 'execute_code'; arguments = @{ action = 'execute'; code = $Code } } } | ConvertTo-Json -Compress -Depth 6
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        $result = Read-Result (Invoke-Mcp $body $SessionId)
        if ($result.success) { return $result.data.result }
        if ($result.data.reason -ne 'no_unity_session') { throw "execute_code failed: $($result | ConvertTo-Json -Compress -Depth 5)" }
        Start-Sleep -Milliseconds 500
    }
    throw 'No Unity session registered with the MCP server.'
}

$init = Invoke-Mcp '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t064-profiler-mcp","version":"1"}}}' $null
$session = $init.Headers['mcp-session-id']
Invoke-Mcp '{"jsonrpc":"2.0","method":"notifications/initialized"}' $session | Out-Null

$role = Invoke-Code 'return System.Diagnostics.Process.GetCurrentProcess().Id.ToString() + " role=" + UnityEditor.MPE.ProcessService.roleName;' $session
if ($Snippet -eq 'main-editor-prepare') {
    # The only snippet for the main editor (empty role), sent before the standalone profiler is opened.
    if ($role -notmatch 'role=$') { throw "main-editor-prepare must run in the main editor; MCP is answered by '$role'." }
}
elseif ($role -notmatch 'role=profiler$') {
    throw "MCP is answered by '$role', not by the Profiler (Standalone Process). Stop the main editor's MCP bridge first."
}
if ($Snippet -eq 'role') { return $role }

$path = Join-Path $PSScriptRoot "profiler-snippets/$Snippet.cs.txt"
$code = (Get-Content -Raw -LiteralPath $path).Replace('__OUT__', $Out)
"$(Get-Date -Format o) [$role] $Snippet -> $(Invoke-Code $code $session)"
