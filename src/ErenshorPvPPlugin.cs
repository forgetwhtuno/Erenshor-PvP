using System;
using Lunaris;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ErenshorPvP
{
    [LunarisPlugin("forgetwhtuno.erenshor.pvp", "0.5.10", "forgetwhtuno",
        "Standalone MMO-style PvP encounters for Erenshor: consensual arranged challenges and rare wild ambushes against off-map Sim proxies, with real player death/respawn.")]
    [LunarisPermission(LunarisPermission.Reflection | LunarisPermission.Harmony)]
    public sealed class ErenshorPvPPlugin : LunarisPlugin
    {
        private Harmony _harmony;
        private bool _runtimeHooksReady;
        private string _runtimeHookFailure = string.Empty;
        private PvpSettings _settings;
        private PvpSuiteAuraProvider _auraProvider;

        private void Awake()
        {
            ErenshorPvPPluginHolder.Instance = this;
            _settings = new PvpSettings();
            Config.Register(ref _settings);
            PvpController.SaveSettings = delegate { try { Config.Save(); } catch { } };
            PvpController.Initialize(_settings);
            _harmony = new Harmony("forgetwhtuno.erenshor.pvp");
            try
            {
                _harmony.PatchAll();
                _runtimeHooksReady = true;
                _runtimeHookFailure = string.Empty;
            }
            catch (Exception ex)
            {
                _runtimeHooksReady = false;
                _runtimeHookFailure = ex.GetType().Name;
                try { _harmony.UnpatchSelf(); } catch { }
                Logging.LogError("Erenshor PvP runtime hooks unavailable (" + _runtimeHookFailure + "). PvP encounters are disabled, but the retained status UI will remain available.");
            }
            PvpController.RuntimeHooksAvailable = _runtimeHooksReady;
            if (_runtimeHooksReady)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
            }

            // Optional Suite Hub transport adapter. Never assumed present; registration failure
            // must never block normal standalone PvP.
            try
            {
                _auraProvider = new PvpSuiteAuraProvider();
                _auraProvider.Register(this);
                PvpController.SuiteBridgeRegistered = _auraProvider.Registered;
            }
            catch (Exception ex) { Logging.LogError("PvP Suite Aura provider setup failed: " + ex); }

            // Derived from the LunarisPlugin attribute so the startup banner can never drift from the
            // actual build again. A hardcoded literal here previously reported "0.5.4" while the running
            // assembly was 0.5.5, which made the startup line useless for confirming which DLL loaded -
            // exactly the check live acceptance depends on.
            Logging.LogInfo("Erenshor PvP " + ResolvePluginVersion() + " loaded. Disabled by default; use the retained PvP panel (or /epvp compatibility commands) to opt in.");
            Logging.LogInfo("PvP runtime marker: plugin_identity=ErenshorPvP; revision=pvp-" + ResolvePluginVersion() + "-world-combat-r2");
        }

        internal bool RuntimeHooksReady { get { return _runtimeHooksReady; } }
        internal string RuntimeHookFailure { get { return _runtimeHookFailure; } }

        // Single source of truth for the displayed version: the LunarisPlugin attribute this class is
        // decorated with. Falls back to "unknown" rather than a stale literal if it cannot be read.
        internal static string ResolvePluginVersion()
        {
            try
            {
                LunarisPluginAttribute attr = Attribute.GetCustomAttribute(
                    typeof(ErenshorPvPPlugin), typeof(LunarisPluginAttribute)) as LunarisPluginAttribute;
                if (attr != null && !string.IsNullOrEmpty(attr.Version)) return attr.Version;
            }
            catch { }
            return "unknown";
        }

        private void Update()
        {
            try { PvpController.Tick(); PvpController.RefreshUi(); }
            catch (Exception ex) { Logging.LogError("PvP update/UI refresh failed: " + ex); PvpPanel.ReleaseDrag(); }
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { PvpController.SceneTransition(); }
        private void OnSceneUnloaded(Scene scene) { PvpController.SceneTransition(); }
        private void OnDestroy()
        {
            // Stop external control first, then tear down retained UI/gameplay-owned temporary state.
            try { if (_auraProvider != null) _auraProvider.Unregister(); } catch { }
            _auraProvider = null;
            PvpController.SuiteBridgeRegistered = false;
            PvpController.RuntimeHooksAvailable = false;
            PvpController.Shutdown();
            PvpController.SaveSettings = null;
            ErenshorPvPPluginHolder.Instance = null;
            try { SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneUnloaded -= OnSceneUnloaded; } catch { }
            try { if (_harmony != null) _harmony.UnpatchSelf(); } catch { }
            _harmony = null;
        }

        internal bool Handle(TypeText typeText, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string command = raw.Trim();
            if (!command.Equals("/epvp", StringComparison.OrdinalIgnoreCase) && !command.StartsWith("/epvp ", StringComparison.OrdinalIgnoreCase)) return false;
            try { if (typeText != null && typeText.typed != null) typeText.typed.text = string.Empty; } catch { }
            return PvpController.HandleCommand(command.Length == 5 ? string.Empty : command.Substring(5).Trim());
        }
    }

    [HarmonyPatch(typeof(TypeText), "CheckCommands")]
    internal static class PvpChatPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(TypeText __instance)
        {
            try { return ErenshorPvPPluginHolder.Instance == null || !ErenshorPvPPluginHolder.Instance.Handle(__instance, __instance == null || __instance.typed == null ? string.Empty : __instance.typed.text); }
            catch { return true; }
        }
    }

    // Harmony patch access is kept separate from the plugin instance so the prefix remains tiny.
    internal static class ErenshorPvPPluginHolder
    {
        internal static ErenshorPvPPlugin Instance;
    }
}
