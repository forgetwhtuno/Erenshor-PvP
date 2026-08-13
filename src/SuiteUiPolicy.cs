using System;
using Lunaris;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ErenshorPvP
{
    /// <summary>
    /// Standalone UI readiness/presence policy shared in shape across the independent suite mods.
    /// It deliberately has no compile-time dependency on Suite Hub and uses only native game
    /// state already proven by the current suite plus a live LunarisPlugin component scan.
    ///
    /// Canonical acquisition policy (Erenshor-Three-Audit-Integration-Handoff,
    /// CONTRACT_RECONCILIATION.md "Readiness contract"): fail closed during character
    /// select/zoning; require live player/scene/manager state; require
    /// PlayerControl.CanMove to become true at least once; then hold ~1 second stable. Once
    /// acquired, ordinary native UI briefly setting CanMove=false does not revoke readiness -
    /// only a character-select/zoning/scene transition resets the acquisition latch.
    /// </summary>
    internal static class SuiteUiPolicy
    {
        private const float StableReadySeconds = 1.0f;
        private const float HubProbeSeconds = 1.0f;
        private const string HubPluginTypeName = "ErenshorSuiteHub.ErenshorSuiteHubPlugin";

        private static bool _acquired;
        private static bool _canMoveSeen;
        private static float _rawReadySince = -1f;
        private static int _readySceneHandle = int.MinValue;
        private static float _nextHubProbe;
        private static bool _hubAvailable;

        internal static bool IsGameplayReady()
        {
            if (GameData.InCharSelect || GameData.Zoning)
            {
                ResetAcquisition();
                return false;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (_readySceneHandle != scene.handle)
            {
                _readySceneHandle = scene.handle;
                ResetAcquisition();
            }

            // Once acquired for this scene, ordinary native UI transitions (e.g. momentary
            // CanMove=false while opening a menu) must not hide suite UI.
            if (_acquired) return true;

            if (!RawPositiveState())
            {
                _rawReadySince = -1f;
                _canMoveSeen = false;
                return false;
            }

            if (!_canMoveSeen)
            {
                try { if (GameData.PlayerControl.CanMove) _canMoveSeen = true; }
                catch { }
                if (!_canMoveSeen) return false;
            }

            if (_rawReadySince < 0f) _rawReadySince = Time.unscaledTime;
            if (Time.unscaledTime - _rawReadySince < StableReadySeconds) return false;

            _acquired = true;
            return true;
        }

        // Matches CONTRACT_RECONCILIATION.md "Contract conflict 5": hide the fallback launcher
        // only once gameplay is ready, Hub is live, AND this module's own Aura bridge actually
        // registered. If Hub is present but this module's bridge failed to register, keep the
        // standalone launcher so the user is never stranded without a GUI.
        internal static bool ShouldShowStandaloneLauncher(bool bridgeRegistered, bool explicitlyVisibleWithHub)
        {
            return IsGameplayReady() && (explicitlyVisibleWithHub || !IsHubAvailable() || !bridgeRegistered);
        }

        internal static bool IsHubAvailable()
        {
            if (Time.unscaledTime < _nextHubProbe) return _hubAvailable;
            _nextHubProbe = Time.unscaledTime + HubProbeSeconds;
            _hubAvailable = false;
            try
            {
                LunarisPlugin[] plugins = UnityEngine.Object.FindObjectsOfType<LunarisPlugin>();
                for (int i = 0; i < plugins.Length; i++)
                {
                    LunarisPlugin plugin = plugins[i];
                    if (plugin == null) continue;
                    Type type = plugin.GetType();
                    if (type != null && string.Equals(type.FullName, HubPluginTypeName, StringComparison.Ordinal))
                    {
                        _hubAvailable = true;
                        break;
                    }
                }
            }
            catch
            {
                // Presence detection is optional. Failure must never hide standalone controls.
                _hubAvailable = false;
            }
            return _hubAvailable;
        }

        internal static void Reset()
        {
            ResetAcquisition();
            _nextHubProbe = 0f;
            _hubAvailable = false;
        }

        private static void ResetAcquisition()
        {
            _acquired = false;
            _canMoveSeen = false;
            _rawReadySince = -1f;
        }

        private static bool RawPositiveState()
        {
            try
            {
                if (GameData.PlayerControl == null || GameData.PlayerControl.Myself == null) return false;
                Character player = GameData.PlayerControl.Myself;
                if (player.MyStats == null || player.gameObject == null || !player.gameObject.activeInHierarchy) return false;

                Scene scene = SceneManager.GetActiveScene();
                if (!scene.IsValid() || !scene.isLoaded) return false;
                // The local Character is persistent and may live in Unity's DontDestroyOnLoad
                // scene, so its GameObject scene must NOT be compared to the active zone scene.

                // These managers are the stronger post-zone readiness evidence already used by
                // Erenshor Follow before it resumes native group-aware travel after scene changes.
                if (GameData.SimMngr == null || GameData.SimPlayerGrouping == null || GameData.GroupMembers == null)
                    return false;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
