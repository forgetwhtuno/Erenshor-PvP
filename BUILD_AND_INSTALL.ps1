param(
    [string]$GameDir = "",
    [string]$LunarisLibDir = "",
    # Compile and report the candidate SHA-256 without touching the installed plugin.
    [switch]$BuildOnly
)

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Game([string]$Explicit) {
    if ($Explicit -and (Test-Path (Join-Path $Explicit "Erenshor.exe"))) { return (Resolve-Path $Explicit).Path }
    $candidates = @()
    if (${env:ProgramFiles(x86)}) { $candidates += Join-Path ${env:ProgramFiles(x86)} "Steam\steamapps\common\Erenshor" }
    if ($env:ProgramFiles) { $candidates += Join-Path $env:ProgramFiles "Steam\steamapps\common\Erenshor" }
    foreach ($candidate in ($candidates | Select-Object -Unique)) { if (Test-Path (Join-Path $candidate "Erenshor.exe")) { return (Resolve-Path $candidate).Path } }
    throw "Erenshor installation not found. Pass -GameDir 'C:\path\to\Erenshor'."
}
function Find-LunarisLibDir([string]$Explicit, [string]$Game) {
    $candidates = @()
    if ($Explicit) { $candidates += $Explicit }
    $candidates += (Join-Path $ScriptRoot "LunarisLibs")
    $candidates += $Game
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (-not $candidate) { continue }
        if ((Test-Path (Join-Path $candidate "Lunaris.dll")) -and (Test-Path (Join-Path $candidate "0Harmony.dll"))) { return (Resolve-Path $candidate).Path }
    }
    throw "Could not find Lunaris developer references. Put Lunaris.dll and 0Harmony.dll in '$ScriptRoot\LunarisLibs' or pass -LunarisLibDir."
}
function Find-Csc { $paths = @("$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe", "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"); foreach ($path in $paths) { if (Test-Path $path) { return $path } }; throw "csc.exe not found." }

$GameDir = Find-Game $GameDir
$LunarisLibDir = Find-LunarisLibDir $LunarisLibDir $GameDir
$csc = Find-Csc; $managed = Join-Path $GameDir "Erenshor_Data\Managed"; $pluginDir = Join-Path $GameDir "plugins"
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
$refs = @((Join-Path $LunarisLibDir "Lunaris.dll"),(Join-Path $LunarisLibDir "0Harmony.dll"),(Join-Path $managed "Assembly-CSharp.dll"),(Join-Path $managed "netstandard.dll"),(Join-Path $managed "UnityEngine.dll"),(Join-Path $managed "UnityEngine.CoreModule.dll"),
    (Join-Path $managed "UnityEngine.UIModule.dll"),(Join-Path $managed "UnityEngine.AIModule.dll"),(Join-Path $managed "UnityEngine.AnimationModule.dll"),(Join-Path $managed "UnityEngine.PhysicsModule.dll"),(Join-Path $managed "UnityEngine.UI.dll"),(Join-Path $managed "UnityEngine.TextRenderingModule.dll"),(Join-Path $managed "UnityEngine.InputLegacyModule.dll"),(Join-Path $managed "Unity.TextMeshPro.dll"))
foreach ($ref in $refs) { if (-not (Test-Path $ref)) { throw "Missing reference: $ref" } }
$out = Join-Path $pluginDir "ErenshorPvP.dll"
# Compile to a staging path first. The previous script compiled straight into <Erenshor>\plugins, so a
# failed/partial compile could leave a broken DLL where Lunaris scans, and an install could silently
# replace the plugin while the game held it loaded.
$buildOutputDir = Join-Path $ScriptRoot "build-output"
New-Item -ItemType Directory -Force -Path $buildOutputDir | Out-Null
$staged = Join-Path $buildOutputDir "ErenshorPvP.dll"
$rsp = Join-Path $env:TEMP "ErenshorPvP.rsp"; $lines = @('/nologo','/target:library','/optimize+',('/out:"{0}"' -f $staged)); $refs | ForEach-Object { $lines += ('/reference:"{0}"' -f $_) }; Get-ChildItem (Join-Path $ScriptRoot "src") -Filter "*.cs" | ForEach-Object { $lines += ('"' + $_.FullName + '"') }
# Cross-mod contract conformance tests, shared with Erenshor Nemesis and Deep Sims. Optional so a
# standalone copy of this mod still builds; the self-test simply covers less without it.
$shared = Join-Path (Split-Path -Parent $ScriptRoot) "shared"
if (Test-Path $shared) { $lines += '/define:SHARED_CONTRACTS'; Get-ChildItem $shared -Filter "*.cs" | ForEach-Object { $lines += ('"' + $_.FullName + '"') } }
$lines | Set-Content $rsp -Encoding ASCII
& $csc "@$rsp"; if ($LASTEXITCODE -ne 0) { throw "Compilation failed." }
if (-not (Test-Path $staged)) { throw "Compiler reported success but produced no assembly at $staged" }
$stagedHash = (Get-FileHash -Algorithm SHA256 -Path $staged).Hash.ToLowerInvariant()
Write-Host "Built Erenshor PvP candidate: $staged" -ForegroundColor Green
Write-Host "  candidate SHA256: $stagedHash"

if ($BuildOnly) {
    if (Test-Path $out) {
        Write-Host "  installed SHA256: $((Get-FileHash -Algorithm SHA256 -Path $out).Hash.ToLowerInvariant())"
    } else { Write-Host "  no DLL currently installed at $out" }
    Write-Host "BuildOnly: nothing installed." -ForegroundColor Yellow
    return
}

if (@(Get-Process -Name "Erenshor" -ErrorAction SilentlyContinue).Count -gt 0) {
    throw "Erenshor is running. Refusing to replace the installed plugin DLL - close the game, or rerun with -BuildOnly."
}
Copy-Item -LiteralPath $staged -Destination $out -Force
$installedHash = (Get-FileHash -Algorithm SHA256 -Path $out).Hash.ToLowerInvariant()
Write-Host "Installed Erenshor PvP to $out" -ForegroundColor Green
Write-Host "  installed SHA256: $installedHash"
if ($installedHash -ne $stagedHash) { throw "Installed DLL hash does not match the built candidate." }

