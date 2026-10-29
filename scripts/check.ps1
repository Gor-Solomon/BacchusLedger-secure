<#
.SYNOPSIS
  Refactor safety net: starts the app on a fresh temporary database, calls /api/selftest and every
  endpoint in scripts/endpoints.txt, and compares status code + raw body with docs/refactor/baseline/.

.EXAMPLE
  scripts/check.ps1 -Baseline   # record the baseline
  scripts/check.ps1             # compare; exit code 1 on any difference
#>
param(
    [switch]$Baseline,
    [string]$Project = "src/BacchusLedger.Api",
    [int]$Port = 5199,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $root $Project
$projectName = Split-Path -Leaf $projectDir
$dll = Join-Path $projectDir "bin/Debug/net8.0/$projectName.dll"
$baselineDir = Join-Path $root "docs/refactor/baseline"
$base = "http://127.0.0.1:$Port"

if (-not $NoBuild) {
    & dotnet build (Join-Path $root "BacchusLedger.sln") -c Debug --nologo -v q
    if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED" -ForegroundColor Red; exit 1 }
}

$tmp = Join-Path ([IO.Path]::GetTempPath()) ("bacchus-check-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp | Out-Null
$outDir = if ($Baseline) { $baselineDir } else { Join-Path $tmp "actual" }
if ($Baseline -and (Test-Path $baselineDir)) { Remove-Item -Recurse -Force $baselineDir }
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# Fresh, deterministic environment for the child process (AI keys blanked so the assistant status is stable).
$envVars = @{
    DB_PATH = (Join-Path $tmp "bacchus.db"); RESET_ON_START = "true"; SIGNING_KEY_PATH = (Join-Path $tmp "demo-key.pem")
    ASPNETCORE_ENVIRONMENT = "Production"; ASPNETCORE_URLS = $base; ASPNETCORE_HTTP_PORTS = ""
    OPENAI_API_KEY = ""; GEMINI_API_KEY = ""; ASSISTANT_PROVIDER = ""; ASSISTANT_ENABLED = ""; OPENAI_MODEL = ""; GEMINI_MODEL = ""
}
$saved = @{}
foreach ($k in $envVars.Keys) { $saved[$k] = [Environment]::GetEnvironmentVariable($k); [Environment]::SetEnvironmentVariable($k, $(if ($envVars[$k]) { $envVars[$k] } else { $null })) }

$app = $null
$failed = 0
try {
    $app = Start-Process dotnet -ArgumentList "`"$dll`"" -WorkingDirectory $projectDir -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $tmp "app.out.log") -RedirectStandardError (Join-Path $tmp "app.err.log")

    Add-Type -AssemblyName System.Net.Http
    $http = New-Object System.Net.Http.HttpClient
    $http.Timeout = [TimeSpan]::FromSeconds(120)

    $up = $false
    for ($i = 0; $i -lt 120 -and -not $up; $i++) {
        if ($app.HasExited) { break }
        try { $up = $http.GetAsync("$base/health").Result.IsSuccessStatusCode } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $up) { Write-Host "App did not start. Log: $tmp" -ForegroundColor Red; Get-Content (Join-Path $tmp "app.out.log") -Tail 30; exit 1 }

    $endpoints = Get-Content (Join-Path $PSScriptRoot "endpoints.txt") | Where-Object { $_.Trim() -and -not $_.TrimStart().StartsWith("#") } |
        ForEach-Object { $p = $_.Trim() -split '\s+', 2; [pscustomobject]@{ Name = $p[0]; Path = $p[1] } }

    $manifest = New-Object System.Collections.Generic.List[string]
    foreach ($e in $endpoints) {
        $res = $http.GetAsync($base + $e.Path).Result
        [IO.File]::WriteAllBytes((Join-Path $outDir "$($e.Name).json"), $res.Content.ReadAsByteArrayAsync().Result)
        $manifest.Add("$($e.Name) $([int]$res.StatusCode) $($e.Path)")
    }
    [IO.File]::WriteAllText((Join-Path $outDir "manifest.txt"), (($manifest -join "`n") + "`n"))

    $self = (Get-Content -Raw -Encoding UTF8 (Join-Path $outDir "selftest.json")) | ConvertFrom-Json
    Write-Host ("selftest: pass={0} passed={1}/{2}" -f $self.pass, $self.passed, $self.total)

    if ($Baseline) { Write-Host "Baseline saved to docs/refactor/baseline ($($endpoints.Count) endpoints)." -ForegroundColor Green; exit 0 }

    $expected = @{}
    foreach ($line in Get-Content (Join-Path $baselineDir "manifest.txt")) { $p = $line.Trim() -split ' ', 3; if ($p.Count -ge 2) { $expected[$p[0]] = $p[1] } }
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    foreach ($line in $manifest) {
        $p = $line -split ' ', 3; $name = $p[0]; $status = $p[1]; $path = $p[2]
        $bf = Join-Path $baselineDir "$name.json"
        if (-not $expected.ContainsKey($name) -or -not (Test-Path $bf)) { Write-Host "FAIL $path : not in baseline" -ForegroundColor Red; $failed++; continue }
        $a = [IO.File]::ReadAllBytes((Join-Path $outDir "$name.json")); $b = [IO.File]::ReadAllBytes($bf)
        $statusOk = $expected[$name] -eq $status
        $bodyOk = $a.Length -eq $b.Length
        $at = -1
        for ($i = 0; $i -lt [Math]::Min($a.Length, $b.Length); $i++) { if ($a[$i] -ne $b[$i]) { $at = $i; $bodyOk = $false; break } }
        if ($at -lt 0 -and -not $bodyOk) { $at = [Math]::Min($a.Length, $b.Length) }
        if ($statusOk -and $bodyOk) { Write-Host "PASS $path" -ForegroundColor Green; continue }
        $failed++
        Write-Host "FAIL $path" -ForegroundColor Red
        if (-not $statusOk) { Write-Host "  status: expected $($expected[$name]), got $status" }
        if (-not $bodyOk) {
            $from = [Math]::Max(0, $at - 80)
            $snip = { param($x) $utf8.GetString($x, $from, [Math]::Min(200, [Math]::Max(0, $x.Length - $from))) }
            Write-Host "  body differs at byte $at (expected $($b.Length) bytes, got $($a.Length))"
            Write-Host "  expected: ...$(& $snip $b)..."
            Write-Host "  actual:   ...$(& $snip $a)..."
        }
    }
    foreach ($name in $expected.Keys) {
        if (-not ($manifest | Where-Object { $_.StartsWith("$name ") })) { Write-Host "FAIL $name : in baseline but not checked" -ForegroundColor Red; $failed++ }
    }
    if ($failed -gt 0) { Write-Host "$failed endpoint(s) differ from the baseline." -ForegroundColor Red; exit 1 }
    Write-Host "ALL PASS ($($manifest.Count) endpoints identical to the baseline)." -ForegroundColor Green
    exit 0
}
finally {
    if ($app -and -not $app.HasExited) { Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue; $app.WaitForExit(5000) | Out-Null }
    foreach ($k in $saved.Keys) { [Environment]::SetEnvironmentVariable($k, $saved[$k]) }
    if ($failed -eq 0) { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue }
    else { Write-Host "Actual responses kept in $outDir" }
}
