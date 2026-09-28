[CmdletBinding()]
param(
    [string] $RawRoot = 'test-results/android/t064/set',
    [int[]] $Runs = @(1, 2, 3),
    [string] $OutCsv = 'specs/001-core-game-foundation/reviews/t064-evidence/profiler-metrics.csv'
)

# T064: summarizes run-N/profiler-counters.csv (one row per profiled player frame) into one row per run.
# Percentiles use the nearest-rank method of the T040 report: value number ceil(n*p) of the n sorted values.
# Every *_ms column is measured under the attached profiler and is distorted; it is never compared with T040.

$ErrorActionPreference = 'Stop'
# Decimal points and CSV output must not depend on the host locale.
[Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::InvariantCulture

function Get-Distinct([object[]] $Rows, [string] $Column) {
    (($Rows | ForEach-Object { [long] $_.$Column } | Sort-Object -Unique) -join '/')
}

function Get-NearestRank([double[]] $Values, [double] $P) {
    $sorted = $Values | Sort-Object
    $rank = [Math]::Max(1, [Math]::Ceiling($sorted.Count * $P))
    $sorted[$rank - 1]
}

function Get-Stats([object[]] $Rows, [string] $Column, [double] $Scale = 1) {
    [double[]] $values = $Rows | ForEach-Object { [double] $_.$Column / $Scale }
    [pscustomobject]@{
        Mean = ($values | Measure-Object -Average).Average
        P50 = Get-NearestRank $values 0.50
        P95 = Get-NearestRank $values 0.95
        Min = ($values | Measure-Object -Minimum).Minimum
        Max = ($values | Measure-Object -Maximum).Maximum
    }
}

$rows = foreach ($run in $Runs) {
    $frames = Import-Csv -LiteralPath (Join-Path $RawRoot "run-$run/profiler-counters.csv")
    $alloc = Get-Stats $frames 'GC_Allocated_In_Frame'
    $count = Get-Stats $frames 'GC_Allocation_In_Frame_Count'
    $main = Get-Stats $frames 'CPU_Main_Thread_Frame_Time' 1e6
    $total = Get-Stats $frames 'CPU_Total_Frame_Time' 1e6
    $gpu = Get-Stats $frames 'GPU_Frame_Time' 1e6
    $last = $frames[-1]
    $byType = Get-Content -LiteralPath (Join-Path $RawRoot "run-$run/profiler-residency-by-type.txt")
    $typeBytes = @{}
    foreach ($line in $byType) {
        if ($line -match '^(\d+) B in (\d+) objects : (.+)$') { $typeBytes[$Matches[3]] = [long] $Matches[1] }
    }
    $residency = Import-Csv -LiteralPath (Join-Path $RawRoot "run-$run/profiler-residency.csv")
    $profilerRt = ($residency | Where-Object { $_.type -eq 'RenderTexture' -and $_.name -like 'ProfilerScreenshot*' } | Measure-Object -Property bytes -Sum).Sum
    $renderTextureCounts = Get-Distinct $frames 'Render_Textures_Count'
    [pscustomobject][ordered]@{
        run = $run
        frames = $frames.Count
        distorted_profiled_frames_per_second = [Math]::Round($frames.Count / 60.0, 2)
        gc_alloc_bytes_per_frame_mean = [Math]::Round($alloc.Mean, 1)
        gc_alloc_bytes_per_frame_p50 = $alloc.P50
        gc_alloc_bytes_per_frame_p95 = $alloc.P95
        gc_alloc_bytes_per_frame_max = $alloc.Max
        gc_alloc_count_per_frame_mean = [Math]::Round($count.Mean, 2)
        gc_alloc_count_per_frame_p50 = $count.P50
        gc_alloc_count_per_frame_p95 = $count.P95
        gc_alloc_count_per_frame_max = $count.Max
        gc_used_bytes_last = $last.GC_Used_Memory
        gc_reserved_bytes_last = $last.GC_Reserved_Memory
        unity_total_used_bytes_last = $last.Total_Used_Memory
        unity_total_reserved_bytes_last = $last.Total_Reserved_Memory
        system_used_bytes_last = $last.System_Used_Memory
        texture_bytes_last = $last.Texture_Memory
        texture_count_last = $last.Texture_Count
        render_texture_bytes_last = $last.Render_Textures_Bytes
        render_texture_counts_seen = $renderTextureCounts
        batches = Get-Distinct $frames 'Batches_Count'
        setpass_calls = Get-Distinct $frames 'SetPass_Calls_Count'
        draw_calls = Get-Distinct $frames 'Draw_Calls_Count'
        triangles = Get-Distinct $frames 'Triangles_Count'
        vertices = Get-Distinct $frames 'Vertices_Count'
        game_objects_last = $last.Game_Object_Count
        objects_last = $last.Object_Count
        snapshot_native_objects = ($byType | Where-Object { $_ -match '^native_objects=' }) -replace 'native_objects=', ''
        snapshot_render_texture_bytes = $typeBytes['RenderTexture']
        snapshot_profiler_screenshot_rt_bytes = $profilerRt
        snapshot_non_profiler_render_texture_bytes = [long] $typeBytes['RenderTexture'] - [long] $profilerRt
        snapshot_texture2d_bytes = $typeBytes['Texture2D']
        snapshot_shader_bytes = $typeBytes['Shader']
        snapshot_material_bytes = $typeBytes['Material']
        distorted_main_thread_ms_p50 = [Math]::Round($main.P50, 2)
        distorted_main_thread_ms_p95 = [Math]::Round($main.P95, 2)
        distorted_cpu_total_ms_p50 = [Math]::Round($total.P50, 2)
        distorted_gpu_ms_p50 = [Math]::Round($gpu.P50, 2)
    }
}

$rows | Export-Csv -LiteralPath $OutCsv -NoTypeInformation -Encoding utf8
$rows | Format-List
