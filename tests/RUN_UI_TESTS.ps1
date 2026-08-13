$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
function Find-Csc {
  foreach ($p in @("$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe", "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe")) { if (Test-Path $p) { return $p } }
  throw "csc.exe not found."
}
$csc=Find-Csc; $out=Join-Path $env:TEMP "ErenshorPvP.UiPolicyTests.exe"
& $csc /nologo /target:exe ("/out:{0}" -f $out) `
  (Join-Path $Root "src\PvpUiGeometry.cs") `
  (Join-Path $Root "src\SuiteLauncherPolicy.cs") `
  (Join-Path $Root "src\PvpHubPresentation.cs") `
  (Join-Path $Root "tests\PvpUiPolicyTests.cs")
if ($LASTEXITCODE -ne 0) { throw "PvP UI policy test compilation failed." }
try { & $out; if ($LASTEXITCODE -ne 0) { throw "PvP UI policy tests failed." } } finally { Remove-Item $out -Force -ErrorAction SilentlyContinue }
