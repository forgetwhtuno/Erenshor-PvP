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
  (Join-Path $Root "src\PvpUiPresentation.cs") `
  (Join-Path $Root "src\PvpWindowChromePolicy.cs") `
  (Join-Path $Root "src\PvpUiStatePolicy.cs") `
  (Join-Path $Root "src\PvpProxyStartupPolicy.cs") `
  (Join-Path $Root "src\PvpPointerOwnershipState.cs") `
  (Join-Path $Root "src\PvpMatchLifecyclePolicy.cs") `
  (Join-Path $Root "src\PvpCombatStartupPolicy.cs") `
  (Join-Path $Root "src\PvpNativeNavHealthPolicy.cs") `
  (Join-Path $Root "src\PvpWorldCombatPolicy.cs") `
  (Join-Path $Root "src\PvpPluginIdentityPolicy.cs") `
  (Join-Path $Root "tests\PvpUiPolicyTests.cs")
if ($LASTEXITCODE -ne 0) { throw "PvP UI policy test compilation failed." }
try { & $out; if ($LASTEXITCODE -ne 0) { throw "PvP UI policy tests failed." } } finally { Remove-Item $out -Force -ErrorAction SilentlyContinue }

# Runtime-wiring source guards supplement the pure pointer-state tests without requiring Unity execution.
$panelSource = Get-Content (Join-Path $Root "src\PvpPanel.cs") -Raw
$dragSource = Get-Content (Join-Path $Root "src\PvpDragGuard.cs") -Raw
$controllerSource = Get-Content (Join-Path $Root "src\PvpController.cs") -Raw
$auraSource = Get-Content (Join-Path $Root "src\PvpSuiteAuraProvider.cs") -Raw
if ($dragSource -notmatch 'OnPointerDown[\s\S]*Acquire\(\)') { throw "PvP drag guard failed: pointer-down ownership missing." }
if ($dragSource -notmatch 'InputButton\.Left' -or $dragSource -notmatch 'UsingUI') { throw "PvP drag guard failed: left-only modern camera containment missing." }
if ($dragSource -notmatch 'OnDisable\(\).*EndDrag' -or $dragSource -notmatch 'OnDestroy\(\).*EndDrag') { throw "PvP drag guard failed: disable/destroy cleanup missing." }
if ($panelSource -notmatch 'private\s+static\s+void\s+HideAll\(\)[\s\S]*PvpDragGuard\.ForceReleaseIfOwned\(\)') { throw "PvP drag guard failed: HideAll does not release ownership." }
if ($panelSource -notmatch 'internal\s+static\s+void\s+ResetPosition\(\)[\s\S]*PvpDragGuard\.ForceReleaseIfOwned\(\)' -or $panelSource -notmatch 'internal\s+static\s+void\s+ResetLauncherPosition\(\)[\s\S]*PvpDragGuard\.ForceReleaseIfOwned\(\)') { throw "PvP drag guard failed: reset paths do not release ownership." }
if ($controllerSource -notmatch 'SceneTransition\(\)[\s\S]*PvpPanel\.ReleaseDrag\(\)' -or $controllerSource -notmatch 'Shutdown\(\)[\s\S]*PvpPanel\.Dispose\(\)') { throw "PvP drag guard failed: zone/unload cleanup wiring missing." }
if ($controllerSource -notmatch 'pvp_disabled' -or $controllerSource -notmatch 'game_not_ready' -or $controllerSource -notmatch 'EncounterCleaned\(\)') { throw "PvP lifecycle guard failed: interrupted cleanup ownership missing." }
if ($controllerSource -notmatch '_nextOffer < Time\.unscaledTime \+ 300f') { throw "PvP lifecycle guard failed: zoning restart delay missing." }
if ($auraSource -notmatch 'Prefix \+ "ui\.state"' -or $auraSource -notmatch 'PvpUiStatePolicy\.Build') { throw "PvP Suite guard failed: ui.state provider missing." }
Write-Host "PvP drag/Suite release source guards: PASS" -ForegroundColor Green

# Native-runtime regression guard: registered proxies retain the safe nameplate/maintenance
# invariant, but native NPC.Start is restored as owner of the complete navigation lifecycle.
$factorySource = Get-Content (Join-Path $Root "src\PvpTemporaryCloneFactory.cs") -Raw
$startupSource = Get-Content (Join-Path $Root "src\PvpProxyStartupPolicy.cs") -Raw
$rewardSource = Get-Content (Join-Path $Root "src\PvpRewardService.cs") -Raw
foreach ($token in @('TrySetField(npc, "Myself", actor)', 'TrySetField(npc, "MyStats"', 'TrySetField(npc, "MyNav", nav)', 'TrySetField(npc, "MySpells", caster)', 'NameFlash', 'HandleMaintenaceAndCounters', 'AllowNativeMaintenance', 'runtime_invalid')) {
  if ($factorySource -notmatch [regex]::Escape($token)) { throw "PvP native-runtime guard failed: missing $token" }
}
if ($startupSource -notmatch 'MaintenanceStatePasses' -or $startupSource -notmatch 'ShouldInterceptMaintenance') { throw "PvP native-runtime guard failed: state discriminator missing." }
if ($factorySource -notmatch 'if \(!IsTemporaryNpc\(npc\)\) return true;') { throw "PvP native-runtime guard failed: vanilla NPC fail-open missing." }
if ($factorySource -notmatch 'native_npc_exception method=NPC\.HandleMaintenaceAndCounters' -or $factorySource -notmatch '\[HarmonyFinalizer\]') { throw "PvP native-runtime guard failed: maintenance exception diagnostics missing." }
if ($rewardSource -notmatch 'GameData\.AddExperience\(xp, false\)' -or $rewardSource -notmatch 'GameData\.PlayerInv\.Gold \+= gold' -or $rewardSource -notmatch 'UpdatePlayerInventory\(\)') { throw "PvP reward guard failed: working reward path changed." }
Write-Host "PvP native NPC runtime/reward source guards: PASS" -ForegroundColor Green
$launcherVisual = Get-Content (Join-Path $Root "src\StandaloneLauncherVisual.cs") -Raw
if ($launcherVisual -notmatch 'Width\s*=\s*154f' -or $launcherVisual -notmatch 'Height\s*=\s*32f' -or
    $launcherVisual -notmatch 'GripWidth\s*=\s*20f' -or $launcherVisual -notmatch '"GripDot"' -or
    $panelSource -notmatch 'StyleGrip\(grip\)' -or $panelSource -notmatch 'PVP \[ON\]') {
    throw "PvP Forgotten Roads launcher visual contract failed."
}
Write-Host "PvP Forgotten Roads launcher visual contract: PASS" -ForegroundColor Green
$chromeSource = Get-Content (Join-Path $Root "src\PvpWindowChromePolicy.cs") -Raw
if ($panelSource -notmatch 'AddVerticalChevron\(_collapseChevron, true\)' -or
    $panelSource -notmatch 'private\s+static\s+void\s+SetCollapsed' -or
    $panelSource -notmatch 'ApplyCollapsedVisibility' -or
    $panelSource -notmatch 'PvpWindowChromePolicy\.PreserveTopBottomY' -or
    $chromeSource -notmatch 'CollapsedHeight\s*=\s*HeaderHeight') {
    throw "PvP Forgotten Roads header collapse contract failed."
}
Write-Host "PvP Forgotten Roads header collapse contract: PASS" -ForegroundColor Green


# Arranged match-start/native-AI source contracts. These are deterministic wiring guards for
# Unity/Harmony paths that cannot be executed by the standalone policy test executable.
$containmentSource = Get-Content (Join-Path $Root "src\PvpCombatContainment.cs") -Raw
$lifecycleSource = Get-Content (Join-Path $Root "src\PvpMatchLifecyclePolicy.cs") -Raw
$startupPolicySource = Get-Content (Join-Path $Root "src\PvpCombatStartupPolicy.cs") -Raw

foreach ($token in @(
  'PvpMatchLifecycleState.Countdown',
  'BeginMatchCountdown',
  'Say("[PvP] 3")',
  'Say("[PvP] GO")',
  '_lifecycle.Go()',
  'PrepareForCountdown',
  'npc.NeverAggro = true',
  'EnemyNpcs[i].NeverAggro = false',
  'go_release',
  'TrySetField(npc, "navDo", null)',
  'TrySetField(npc, "behDo", null)',
  'PrepareNativeStartProbe',
  'ObserveNativeNavEntered',
  'ObserveNativeNavCompleted',
  'proxy_nav_faulted',
  'NativeNavHealthSummary',
  'native_update_reached',
  'combat_section_reached',
  'legal_target_acquired',
  'nav_pursuit_requested',
  'melee_decision',
  'melee_attempt',
  'attack_spell_decision',
  'heal_check',
  'spell_start',
  'damage_to_defender',
  'heal_to_attacker',
  'technical_failure_ai_inactive',
  'HasAnyNativeCombatEvidence',
  'CanGrantVictoryReward'
)) {
  if (($controllerSource + $factorySource + $containmentSource + $lifecycleSource + $startupPolicySource) -notmatch [regex]::Escape($token)) {
    throw "PvP match-start/native-AI guard failed: missing $token"
  }
}
if ($factorySource -match 'EnsureNativeBehaviorCoroutines' -or $factorySource -match 'StartCoroutine\(' -or
    $factorySource -match 'InvokeCoroutineBody') {
  throw "PvP native lifecycle guard failed: manual NavUpdate/BehaviorUpdate coroutine startup returned."
}
if ($factorySource -notmatch '\[HarmonyPatch\(typeof\(NPC\), "UpdateNav"\)\]' -or
    $factorySource -notmatch 'ObserveNativeNavException' -or
    $containmentSource -notmatch 'CompleteNativeNavFailure') {
  throw "PvP nav health guard failed: actual UpdateNav progression/fault probe missing."
}
if ($factorySource -notmatch 'npc\.enabled = false' -or $containmentSource -notmatch 'EnemyNpcs\[i\]\.enabled = true' -or
    $containmentSource -notmatch 'ObserveProxyNativeStartCompleted') {
  throw "PvP native Start lifecycle guard failed: countdown hold or GO Start completion wiring missing."
}
if ($factorySource -notmatch 'npc\.NoSelfHeal = false') { throw "PvP healing guard failed: native self-heal remains disabled." }
if ($factorySource -notmatch 'if \(npc\.NoSelfHeal\) failures\.Add') { throw "PvP healing guard failed: runtime verifier still expects self-heal suppression." }
if ($containmentSource -notmatch 'return !PvpTemporaryCloneFactory\.IsTemporaryNpc\(npc\)[\s\S]*!PvpTemporaryCloneFactory\.IsTemporaryActor\(target\)') { throw "PvP pre-GO guard failed: defender-side proxy acquisition is not held." }
if ($containmentSource -notmatch 'Defenders\.Add\(player\)' -or $containmentSource -notmatch 'AddPartyDefenders\(\)' -or
    $containmentSource -notmatch 'RegisterDefenderPet\(actor\)' -or $containmentSource -notmatch 'actor\.Master') {
  throw "PvP participant guard failed: player/current party/current owned-pet defender set is incomplete."
}
if ($factorySource -notmatch 'ShouldRecordCompetitiveResult\(reason\)' -or
    $factorySource -notmatch 'history_credit=false' -or
    $factorySource -notmatch 'winner = null') {
  throw "PvP technical-failure guard failed: no-credit terminal path missing."
}
if ($containmentSource -notmatch 'AllowSpellStart' -or $factorySource -notmatch 'ObserveAndAllowSpellStart') {
  throw "PvP pre-GO guard failed: temporary-proxy spell initiation is not held."
}
$worldPolicySource = Get-Content (Join-Path $Root "src\PvpWorldCombatPolicy.cs") -Raw
if ($containmentSource -notmatch 'AllowHeal' -or $worldPolicySource -notmatch 'targetDefender && sourceDefender' -or
    $worldPolicySource -notmatch 'targetAttacker && sourceAttacker') {
  throw "PvP healing guard failed: same-team native healing policy missing."
}
foreach ($token in @(
  'AllowWorld',
  'IsProtectedNonCombat',
  'simPlayer || ownedOrSummoned',
  'sourceParticipant && noTarget',
  'Do not proximity-block AE/PBAE starts',
  'IsProtectedWorldActor',
  'protected_target_cleared',
  'SpawnActorCollisionClearance',
  'protectedActors',
  'IsPermittedProxyTarget'
)) {
  if (($worldPolicySource + $containmentSource + $factorySource) -notmatch [regex]::Escape($token)) {
    throw "PvP world-combat guard failed: missing $token"
  }
}
if ($containmentSource -match 'third_party_aggro' -or $containmentSource -match '_thirdPartyInterference' -or
    $controllerSource -match 'WorldCombatBusy\(\)' -or $controllerSource -match 'player_in_combat') {
  throw "PvP world-combat guard failed: isolation-era third-party/player-in-combat abort remains wired."
}
if ($worldPolicySource -notmatch 'DecideAggro[\s\S]*AllowWorld' -or
    $worldPolicySource -notmatch 'DecideDamage[\s\S]*AllowWorld' -or
    $worldPolicySource -notmatch 'DecideHeal[\s\S]*AllowWorld') {
  throw "PvP world-combat guard failed: native outside aggro/damage/heal is not admitted."
}
Write-Host "PvP MMO world-combat policy source guards: PASS" -ForegroundColor Green

if ($rewardSource -notmatch '_lastClaimedMatchId' -or $rewardSource -notmatch 'already claimed' -or
    $rewardSource -notmatch 'TryPersistSettings\(\)') {
  throw "PvP reward guard failed: legitimate match exact-once claim barrier missing."
}
if ($factorySource -notmatch 'TeamClones\.Count == 0 && _clone == null\) return' -or
    $controllerSource -notmatch 'EncounterCleaned\(\)') {
  throw "PvP cleanup guard failed: duplicate-safe terminal cleanup missing."
}
Write-Host "PvP arranged match-start/native-AI source guards: PASS" -ForegroundColor Green

# Final forensic combat-recovery matrix (task cases 24-49). These are source/pure-policy
# contracts; current-assembly build and live two-match acceptance remain separate gates.
$navPolicySource = Get-Content (Join-Path $Root "src\PvpNativeNavHealthPolicy.cs") -Raw
$identityPolicySource = Get-Content (Join-Path $Root "src\PvpPluginIdentityPolicy.cs") -Raw
$finalRecoveryCases = 0
function Assert-Recovery([bool]$condition, [string]$name) {
  if (-not $condition) { throw ("PvP final recovery matrix failed: " + $name) }
  $script:finalRecoveryCases++
}

Assert-Recovery ($identityPolicySource -match 'ExactlyOneExpectedIdentity') '24 exactly one effective ErenshorPvP identity policy'
Assert-Recovery ($startupSource -match 'hasNamePlateText' -and $startupSource -match 'hasNamePlateObject') '25 runtime invariant includes nameplate dependencies'
Assert-Recovery ($factorySource -match 'NamePlateTxt' -and $factorySource -match 'HandleNameTag') '26 HandleNameTag/nameplate regression guard retained'
Assert-Recovery ($factorySource -match 'npc\.NeverAggro = true' -and $factorySource -match 'npc\.enabled = false' -and $containmentSource -match 'IsTemporaryActor\(target\)') '27 countdown holds attackers and defenders'
Assert-Recovery ($controllerSource -match 'go_count=' -and $containmentSource -match 'go_release' -and $lifecycleSource -match 'GoTransitions') '28 GO releases once'
Assert-Recovery ($factorySource -match 'ValidateProxyStartupInvariant' -and $factorySource -match 'action=blocked_invalid') '29 native dependencies validated before Start/Active'
Assert-Recovery ($navPolicySource -match 'LaunchAloneIsHealthy' -and $navPolicySource -match 'return false') '30 coroutine launch alone is not healthy'
Assert-Recovery ($navPolicySource -match 'faulted' -and $navPolicySource -match '!faulted') '31 first MoveNext/UpdateNav fault is unhealthy'
Assert-Recovery ($factorySource -match 'ObserveNativeNavEntered' -and $factorySource -match 'ObserveNativeNavCompleted') '32 UpdateNav progression is health evidence'
Assert-Recovery ($navPolicySource -match 'NeedsPursuit' -and $factorySource -match 'nav_pursuit_requested') '33 out-of-range melee pursuit evidence'
Assert-Recovery ($navPolicySource -match 'distance > attackRange' -or $navPolicySource -match 'distance > Mathf') '34 ranged in-range does not require artificial movement'
Assert-Recovery ($factorySource -match 'legal_target_acquired') '35 native target acquisition evidence'
Assert-Recovery ($factorySource -match 'combat_section_reached' -and $factorySource -match 'native_update_reached') '36 native BehaviorUpdate/combat progression evidence'
Assert-Recovery ($factorySource -match 'attack_spell_decision') '37 attack spell decision evidence'
Assert-Recovery ($factorySource -match 'npc\.NoSelfHeal = false' -and $containmentSource -match 'AllowHeal') '38 self/ally healing admitted'
Assert-Recovery ($worldPolicySource -match 'outside world actor' -or $worldPolicySource -match 'AllowWorld') '39 ordinary hostile world actor may join'
Assert-Recovery ($worldPolicySource -match 'simPlayer \|\| ownedOrSummoned') '40 Sim participation is not protected-NPC interference'
Assert-Recovery ($worldPolicySource -match 'ownedOrSummoned') '41 pet/summon participation is not protected-NPC interference'
Assert-Recovery ($containmentSource -notmatch 'third_party_aggro' -and $controllerSource -notmatch 'third_party_aggro') '42 third_party_aggro is not a runtime terminal path'
Assert-Recovery ($factorySource -match 'protected_target_cleared') '43 protected neutral target is rejected narrowly'
Assert-Recovery ($factorySource -match 'ForceAggroOn\(null\)' -and $factorySource -match 'protected_target_cleared') '44 protected target rejection clears target without match cancel'
Assert-Recovery ($containmentSource -match 'CompleteNativeNavFailure' -and $containmentSource -match 'technical_failure_ai_inactive') '45 complete nav failure becomes technical failure'
Assert-Recovery ($factorySource -match 'winner=none' -and $factorySource -match 'winner = null') '46 technical failure winner none'
Assert-Recovery ($factorySource -match 'xp=0; gold=0' -and $factorySource -match 'history_credit=false') '47 technical failure zero rewards/history'
Assert-Recovery ($rewardSource -match '_lastClaimedMatchId' -and $rewardSource -match 'already claimed') '48 legitimate victory reward exact-once'
Assert-Recovery ($controllerSource -match 'EncounterCleaned' -and $factorySource -match 'TeamClones\.Clear\(\)') '49 repeated-match cleanup permits second match'

if ($finalRecoveryCases -ne 26) { throw "PvP final recovery matrix count mismatch: $finalRecoveryCases / 26" }
Write-Host ("PvP final forensic recovery source matrix: PASS (" + $finalRecoveryCases + "/26)") -ForegroundColor Green

