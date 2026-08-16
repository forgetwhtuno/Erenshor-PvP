using System;
using System.Text;
using Lunaris;
using Lunaris.IPC;

namespace ErenshorPvP
{
    // Thin, optional Lunaris Aura transport adapter over the authoritative PvpControlApi.
    // Erenshor-Three-Audit-Integration-Handoff/CONTRACT_RECONCILIATION.md: Hub speaks Aura only
    // and never reflects into private mod state; this class owns nothing beyond
    // formatting/parsing the bounded wire payloads and forwarding to PvpControlApi. No
    // compile-time reference to ErenshorSuiteHub.dll, no gameplay logic duplicated here.
    internal sealed class PvpSuiteAuraProvider
    {
        private const string Prefix = "forgetwhtuno.erenshor.suite." + PvpControlApi.ModuleId + ".v1.";

        private IAuraProvider<string> _describe;
        private IAuraProvider<string> _basicSettings;
        private IAuraProvider<string> _uiState;
        private IAuraProvider<string, string, string> _settingSet;
        private IAuraProvider<string, string, string> _action;
        private string _version = "0.0.0";
        private ILog _log;

        internal bool Registered { get; private set; }

        internal void Register(LunarisPlugin owner)
        {
            if (owner == null) return;
            _log = owner.Logging;
            try
            {
                LunarisPluginAttribute attr = Attribute.GetCustomAttribute(owner.GetType(), typeof(LunarisPluginAttribute)) as LunarisPluginAttribute;
                if (attr != null && !string.IsNullOrEmpty(attr.Version)) _version = attr.Version;

                _describe = owner.IPCAuraProvider<string>(Prefix + "describe");
                _describe.RegisterFunc(Describe);

                _basicSettings = owner.IPCAuraProvider<string>(Prefix + "settings.basic");
                _basicSettings.RegisterFunc(BasicSettings);

                _uiState = owner.IPCAuraProvider<string>(Prefix + "ui.state");
                _uiState.RegisterFunc(UiState);

                _settingSet = owner.IPCAuraProvider<string, string, string>(Prefix + "setting.set");
                _settingSet.RegisterFunc(SetSetting);

                _action = owner.IPCAuraProvider<string, string, string>(Prefix + "action");
                _action.RegisterFunc(InvokeAction);

                Registered = true;
            }
            catch (Exception ex)
            {
                Registered = false;
                if (_log != null) { try { _log.LogError("[Erenshor PvP] Suite Aura provider registration failed: " + ex.GetType().Name); } catch { } }
                Unregister();
            }
        }

        // Provider lifecycle contract: explicitly unregister every Aura handler on OnDestroy so
        // Hub sees this module disappear immediately rather than calling into a torn-down plugin.
        internal void Unregister()
        {
            SafeUnregister(_describe); _describe = null;
            SafeUnregister(_basicSettings); _basicSettings = null;
            SafeUnregister(_uiState); _uiState = null;
            SafeUnregister(_settingSet); _settingSet = null;
            SafeUnregister(_action); _action = null;
            Registered = false;
        }

        private static void SafeUnregister(IAuraProvider provider)
        {
            if (provider == null) return;
            try { provider.UnregisterFunc(); } catch { }
        }

        private string Describe()
        {
            try
            {
                PvpControlState s = PvpControlApi.GetBasicState();
                StringBuilder sb = new StringBuilder(256);
                AppendField(sb, "protocol", "1");
                AppendField(sb, "module", PvpControlApi.ModuleId);
                AppendField(sb, "display", "Erenshor PvP");
                AppendField(sb, "version", _version);
                AppendField(sb, "summary", s.Enabled ? "World PvP enabled" : "World PvP disabled");
                AppendField(sb, "status", PvpControlApi.GetStatus());
                if (s.ProtectedHere) AppendField(sb, "warning", "Protected zone - PvP disabled here");
                AppendField(sb, "actions", "openPanel,closePanel,resetPanel,resetLauncher");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "protocol=1&module=" + PvpControlApi.ModuleId + "&display=Erenshor+PvP&version=" +
                    Uri.EscapeDataString(_version) + "&warning=" + Uri.EscapeDataString(ex.GetType().Name);
            }
        }

        private string UiState()
        {
            try
            {
                return PvpUiStatePolicy.Build(PvpControlApi.ModuleId, PvpController.PanelOpen,
                    PvpPanel.CanvasSortOrder, PvpPanel.LastActivatedAt);
            }
            catch { return string.Empty; }
        }

        private string BasicSettings()
        {
            try
            {
                PvpControlState s = PvpControlApi.GetBasicState();
                StringBuilder sb = new StringBuilder(256);
                AppendBoolSettingLine(sb, "enabled", "PvP Enabled", s.Enabled);
                AppendBoolSettingLine(sb, "showLauncher", "Show PvP launcher", s.ShowLauncher);
                AppendBoolSettingLine(sb, "arranged", "Arranged challenges", s.ArrangedEnabled);
                AppendBoolSettingLine(sb, "ambush", "Wild ambushes", s.AmbushEnabled);
                return sb.ToString();
            }
            catch { return string.Empty; }
        }

        // Every mutating call is revalidated by PvpControlApi/PvpController; Hub is not
        // authorization. "ok" means accepted, not necessarily synchronously reflected in the
        // panel - Hub re-reads state on a later frame via describe/settings.basic.
        private string SetSetting(string settingId, string value)
        {
            try
            {
                bool boolValue = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                if (string.Equals(settingId, "enabled", StringComparison.Ordinal))
                    return PvpControlApi.SetEnabled(boolValue) ? "ok" : "rejected";
                if (string.Equals(settingId, "showLauncher", StringComparison.Ordinal))
                    return PvpControlApi.SetShowLauncher(boolValue) ? "ok" : "rejected";
                if (string.Equals(settingId, "arranged", StringComparison.Ordinal))
                    return PvpControlApi.SetArrangedEnabled(boolValue) ? "ok" : "rejected";
                if (string.Equals(settingId, "ambush", StringComparison.Ordinal))
                    return PvpControlApi.SetAmbushEnabled(boolValue) ? "ok" : "rejected";
                return "unknown setting";
            }
            catch (Exception ex) { return "error:" + ex.GetType().Name; }
        }

        private string InvokeAction(string actionId, string argument)
        {
            try
            {
                if (string.Equals(actionId, "openPanel", StringComparison.Ordinal))
                    return PvpControlApi.OpenPanel() ? "ok" : "rejected";
                if (string.Equals(actionId, "closePanel", StringComparison.Ordinal))
                    return PvpControlApi.ClosePanel() ? "ok" : "rejected";
                if (string.Equals(actionId, "resetPanel", StringComparison.Ordinal))
                    return PvpControlApi.ResetPanelPosition() ? "ok" : "rejected";
                if (string.Equals(actionId, "resetLauncher", StringComparison.Ordinal))
                    return PvpControlApi.ResetLauncherPosition() ? "ok" : "rejected";
                return "unknown action";
            }
            catch (Exception ex) { return "error:" + ex.GetType().Name; }
        }

        private static void AppendField(StringBuilder sb, string key, string value)
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append(key).Append('=').Append(Uri.EscapeDataString(value ?? string.Empty));
        }

        private static void AppendBoolSettingLine(StringBuilder sb, string id, string label, bool value)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append("id=").Append(Uri.EscapeDataString(id));
            sb.Append("&label=").Append(Uri.EscapeDataString(label));
            sb.Append("&tier=basic&type=bool&value=").Append(value ? "true" : "false");
            sb.Append("&mutable=true");
        }
    }
}
