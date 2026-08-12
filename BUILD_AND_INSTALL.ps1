param(
    [string]$GameDir = "",
    [string]$BepInExRoot = ""
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
function Find-Roots([string]$Explicit, [string]$Game) {
    if ($Explicit -and (Test-Path (Join-Path $Explicit "BepInEx\core\BepInEx.dll"))) { return ,(Resolve-Path $Explicit).Path }
    $roots = @(); if (Test-Path (Join-Path $Game "BepInEx\core\BepInEx.dll")) { $roots += (Resolve-Path $Game).Path }
    $profiles = Join-Path $env:APPDATA "r2modmanPlus-local\Erenshor\profiles"
    if (Test-Path $profiles) { Get-ChildItem $profiles -Directory | ForEach-Object { if (Test-Path (Join-Path $_.FullName "BepInEx\core\BepInEx.dll")) { $roots += $_.FullName } } }
    return @($roots | Select-Object -Unique)
}
function Find-Csc { $paths = @("$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe", "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"); foreach ($path in $paths) { if (Test-Path $path) { return $path } }; throw "csc.exe not found." }

$GameDir = Find-Game $GameDir; $roots = @(Find-Roots $BepInExRoot $GameDir)
if ($roots.Count -eq 0) { throw "No BepInEx profile found." }
if ($roots.Count -eq 1) { $InstallRoot = $roots[0] } else { $InstallRoot = $roots[0]; Write-Host "Multiple profiles found; using $InstallRoot. Pass -BepInExRoot to choose explicitly." }
$csc = Find-Csc; $managed = Join-Path $GameDir "Erenshor_Data\Managed"; $core = Join-Path $InstallRoot "BepInEx\core"; $pluginDir = Join-Path $InstallRoot "BepInEx\plugins\ErenshorPvP"
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
$refs = @((Join-Path $core "BepInEx.dll"),(Join-Path $core "0Harmony.dll"),(Join-Path $managed "Assembly-CSharp.dll"),(Join-Path $managed "netstandard.dll"),(Join-Path $managed "UnityEngine.dll"),(Join-Path $managed "UnityEngine.CoreModule.dll"),(Join-Path $managed "UnityEngine.AIModule.dll"),(Join-Path $managed "UnityEngine.AnimationModule.dll"),(Join-Path $managed "UnityEngine.PhysicsModule.dll"),(Join-Path $managed "UnityEngine.UI.dll"),(Join-Path $managed "UnityEngine.IMGUIModule.dll"),(Join-Path $managed "UnityEngine.TextRenderingModule.dll"),(Join-Path $managed "UnityEngine.InputLegacyModule.dll"))
foreach ($ref in $refs) { if (-not (Test-Path $ref)) { throw "Missing reference: $ref" } }
$out = Join-Path $pluginDir "ErenshorPvP.dll"; $rsp = Join-Path $env:TEMP "ErenshorPvP.rsp"; $lines = @('/nologo','/target:library','/optimize+',('/out:"{0}"' -f $out)); $refs | ForEach-Object { $lines += ('/reference:"{0}"' -f $_) }; Get-ChildItem (Join-Path $ScriptRoot "src") -Filter "*.cs" | ForEach-Object { $lines += ('"' + $_.FullName + '"') }
# Cross-mod contract conformance tests, shared with Erenshor Nemesis and Deep Sims. Optional so a
# standalone copy of this mod still builds; the self-test simply covers less without it.
$shared = Join-Path (Split-Path -Parent $ScriptRoot) "shared"
if (Test-Path $shared) { $lines += '/define:SHARED_CONTRACTS'; Get-ChildItem $shared -Filter "*.cs" | ForEach-Object { $lines += ('"' + $_.FullName + '"') } }
$lines | Set-Content $rsp -Encoding ASCII
& $csc "@$rsp"; if ($LASTEXITCODE -ne 0) { throw "Compilation failed." }
Write-Host "Installed Erenshor PvP to $out" -ForegroundColor Green
