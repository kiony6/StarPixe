# StarPixelSurge Windows local deploy & verification
#
# Runs the Unity-side checks against the local project in batchmode:
#   1. compile  - script compilation verify (batchmode compiles first; failure = non-zero exit)
#   2. validate - config database validation (skills/affixes/combos/classes/waves/map themes)
#   3. simulation - deterministic in-process network simulation
#   4. udp      - real UDP two-process playtest (builds a Player, host + clients, host migration)
#
# Usage (PowerShell, from the repo root):
#   powershell -ExecutionPolicy Bypass -File tools\local-deploy.ps1
#   powershell -ExecutionPolicy Bypass -File tools\local-deploy.ps1 -Target compile
#   powershell -ExecutionPolicy Bypass -File tools\local-deploy.ps1 -UnityPath "C:\...\Unity.exe"
#
# If Unity is not found from UNITY_BIN / Unity Hub defaults, pass -UnityPath explicitly.

[CmdletBinding()]
param(
    [ValidateSet('all', 'compile', 'validate', 'simulation', 'udp')]
    [string]$Target = 'all',

    [string]$UnityPath = $env:UNITY_BIN,

    [int]$UdpClients = 2,
    [int]$UdpDuration = 12,
    [double]$UdpLatency = 0.08,
    [double]$UdpJitter = 0.03,
    [double]$UdpLossRate = 0.05,

    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$LogDir = Join-Path $RepoRoot 'Logs'
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Get-UnityVersion {
    $versionFile = Join-Path $RepoRoot 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path $versionFile)) {
        return $null
    }

    $line = Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(\S+)' | Select-Object -First 1
    if ($line) {
        return $line.Matches[0].Groups[1].Value
    }

    return $null
}

function Resolve-Unity {
    if ($UnityPath -and (Test-Path $UnityPath)) {
        return (Resolve-Path $UnityPath).Path
    }

    $version = Get-UnityVersion
    $candidates = @()

    $roots = @()
    if ($env:ProgramFiles) { $roots += $env:ProgramFiles }
    if (${env:ProgramFiles(x86)}) { $roots += ${env:ProgramFiles(x86)} }

    foreach ($root in $roots) {
        if ($version) {
            $candidates += (Join-Path $root "Unity\Hub\Editor\$version\Editor\Unity.exe")
        }

        $candidates += (Join-Path $root 'Unity\Hub\Editor\*\Editor\Unity.exe')
        $candidates += (Join-Path $root 'Unity\Editor\Unity.exe')
    }

    foreach ($candidate in $candidates) {
        $resolved = Get-ChildItem -Path $candidate -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($resolved) {
            return $resolved.FullName
        }
    }

    return $null
}

function Invoke-UnityStage {
    param(
        [string]$Name,
        [string]$ExecuteMethod,
        [string[]]$ExtraArgs = @()
    )

    $logFile = Join-Path $LogDir "$Name.log"
    Write-Host "==> $Name (log: Logs/$Name.log)" -ForegroundColor Cyan

    $unityArgs = @(
        '-batchmode', '-quit', '-nographics',
        '-projectPath', $RepoRoot,
        '-logFile', $logFile,
        '-executeMethod', $ExecuteMethod
    ) + $ExtraArgs

    $proc = Start-Process -FilePath $script:Unity -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
    $code = $proc.ExitCode

    if ($code -eq 0) {
        Write-Host "    PASS" -ForegroundColor Green
    } else {
        Write-Host "    FAIL (exit $code)" -ForegroundColor Red
    }

    return $code
}

$script:Unity = Resolve-Unity
if (-not $script:Unity) {
    Write-Host "Unity executable not found." -ForegroundColor Red
    Write-Host "Pass -UnityPath, or set UNITY_BIN, e.g.:" -ForegroundColor Yellow
    Write-Host '  -UnityPath "C:\Program Files\Unity\Hub\Editor\2022.3.55f1\Editor\Unity.exe"' -ForegroundColor Yellow
    exit 2
}

Write-Host "Unity:   $script:Unity"
Write-Host "Project: $RepoRoot"
Write-Host "Target:  $Target"
Write-Host ''

$results = [ordered]@{}

switch ($Target) {
    'compile' {
        $results['compile'] = Invoke-UnityStage -Name 'compile' `
            -ExecuteMethod 'StarPixelSurge.EditorTools.ScriptCompileVerifier.VerifyFromCommandLine'
    }
    'validate' {
        $results['validate'] = Invoke-UnityStage -Name 'validate' `
            -ExecuteMethod 'StarPixelSurge.EditorTools.StarPixelSurgeSetup.ValidateFromCommandLine'
    }
    'simulation' {
        $results['simulation'] = Invoke-UnityStage -Name 'simulation' `
            -ExecuteMethod 'StarPixelSurge.EditorTools.NetworkSimulationRunner.RunFromCommandLine'
    }
    'udp' {
        $udpArgs = @(
            '-playtestClients', "$UdpClients",
            '-playtestDuration', "$UdpDuration",
            '-playtestLatency', "$UdpLatency",
            '-playtestJitter', "$UdpJitter",
            '-playtestLossRate', "$UdpLossRate"
        )
        if ($SkipBuild) { $udpArgs += '-playtestSkipBuild' }
        $results['udp'] = Invoke-UnityStage -Name 'udp' `
            -ExecuteMethod 'StarPixelSurge.EditorTools.UdpTwoProcessTestRunner.RunFromCommandLine' `
            -ExtraArgs $udpArgs
    }
    'all' {
        $results['compile'] = Invoke-UnityStage -Name 'compile' `
            -ExecuteMethod 'StarPixelSurge.EditorTools.ScriptCompileVerifier.VerifyFromCommandLine'
        if ($results['compile'] -ne 0) {
            Write-Host 'compile failed; skipping later stages.' -ForegroundColor Red
        } else {
            $results['validate'] = Invoke-UnityStage -Name 'validate' `
                -ExecuteMethod 'StarPixelSurge.EditorTools.StarPixelSurgeSetup.ValidateFromCommandLine'
            $results['simulation'] = Invoke-UnityStage -Name 'simulation' `
                -ExecuteMethod 'StarPixelSurge.EditorTools.NetworkSimulationRunner.RunFromCommandLine'

            $udpArgs = @(
                '-playtestClients', "$UdpClients",
                '-playtestDuration', "$UdpDuration",
                '-playtestLatency', "$UdpLatency",
                '-playtestJitter', "$UdpJitter",
                '-playtestLossRate', "$UdpLossRate"
            )
            if ($SkipBuild) { $udpArgs += '-playtestSkipBuild' }
            $results['udp'] = Invoke-UnityStage -Name 'udp' `
                -ExecuteMethod 'StarPixelSurge.EditorTools.UdpTwoProcessTestRunner.RunFromCommandLine' `
                -ExtraArgs $udpArgs
        }
    }
}

Write-Host ''
Write-Host '=== Summary ===' -ForegroundColor Cyan
$failed = 0
foreach ($entry in $results.GetEnumerator()) {
    $state = if ($entry.Value -eq 0) { 'PASS' } else { 'FAIL' }
    $color = if ($entry.Value -eq 0) { 'Green' } else { 'Red' }
    Write-Host ("  {0,-12} {1}" -f $entry.Key, $state) -ForegroundColor $color
    if ($entry.Value -ne 0) { $failed++ }
}

if ($failed -gt 0) {
    Write-Host ''
    Write-Host "Failed stages: $failed. Inspect Logs\*.log for details." -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host 'All requested checks passed.' -ForegroundColor Green
exit 0
