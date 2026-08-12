using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ErenshorPvP
{
    [BepInPlugin("forgetwhtuno.erenshor.pvp", "Erenshor PvP", "0.4.0")]
    [BepInProcess("Erenshor.exe")]
    public sealed class ErenshorPvPPlugin : BaseUnityPlugin
    {
        private Harmony _harmony;

        private void Awake()
        {
            ErenshorPvPPluginHolder.Instance = this;
            PvpController.Initialize(Config);
            _harmony = new Harmony("forgetwhtuno.erenshor.pvp");
            _harmony.PatchAll();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Logger.LogInfo("Erenshor PvP 0.4.0 loaded. Disabled by default; use /epvp or F10 for world PvP.");
        }

        private void Update() { try { PvpController.Tick(); } catch (Exception ex) { Logger.LogError("PvP update failed: " + ex); } }
        private void OnGUI() { try { PvpController.Draw(); } catch (Exception ex) { Logger.LogError("PvP UI failed: " + ex); } }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { PvpController.SceneTransition(); }
        private void OnSceneUnloaded(Scene scene) { PvpController.SceneTransition(); }
        private void OnDestroy() { PvpController.Shutdown(); ErenshorPvPPluginHolder.Instance = null; try { SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneUnloaded -= OnSceneUnloaded; } catch { } try { if (_harmony != null) _harmony.UnpatchSelf(); } catch { } }

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

    // IMGUI cannot swallow this on its own: Erenshor reads the raw mouse here rather than
    // through Event.current, so a click on the PvP panel would otherwise also move the
    // camera or drop the current target.
    [HarmonyPatch(typeof(PlayerControl), "LeftClick")]
    internal static class PvpPanelLeftClickPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            try { return !PvpController.PointerIsOverUi(); }
            catch { return true; }
        }
    }

    // csMouseOrbit.LateUpdate reads Input.GetAxis("Mouse X"/"Mouse Y") every frame with no
    // mouse-button gate, so any pointer movement turns the camera - including dragging a
    // panel. Skipping LateUpdate outright would also stop the camera following the player,
    // so instead the orbit speeds are zeroed for the duration of the call: the follow and
    // distance logic still runs, but the axis deltas contribute nothing.
    [HarmonyPatch(typeof(csMouseOrbit), "LateUpdate")]
    internal static class PvpCameraLookPatch
    {
        private static csMouseOrbit _muted;
        private static float _mutedX;
        private static float _mutedY;

        // Always restores before muting again, so a throw inside LateUpdate cannot strand
        // the camera at zero sensitivity.
        internal static void Restore()
        {
            csMouseOrbit orbit = _muted;
            _muted = null;
            if (orbit == null) return;
            try { orbit.xSpeed = _mutedX; orbit.ySpeed = _mutedY; } catch { }
        }

        [HarmonyPrefix]
        private static void Prefix(csMouseOrbit __instance)
        {
            Restore();
            try
            {
                if (__instance == null || !PvpController.PointerIsOverUi()) return;
                _mutedX = __instance.xSpeed;
                _mutedY = __instance.ySpeed;
                __instance.xSpeed = 0f;
                __instance.ySpeed = 0f;
                _muted = __instance;
            }
            catch { }
        }

        [HarmonyPostfix]
        private static void Postfix() { Restore(); }
    }

    // Harmony patch access is kept separate from the plugin instance so the prefix remains tiny.
    internal static class ErenshorPvPPluginHolder
    {
        internal static ErenshorPvPPlugin Instance;
    }
}
