using BepInEx.Configuration;

namespace ErenshorPvP
{
    internal static class PvpRecordService
    {
        private static ConfigEntry<int> _wins;
        private static ConfigEntry<int> _losses;
        private static ConfigEntry<int> _escapes;
        private static ConfigEntry<string> _lastOpponent;
        private static ConfigEntry<string> _lastResult;
        private static ConfigEntry<int> _arrangedWins;
        private static ConfigEntry<int> _arrangedLosses;
        private static ConfigEntry<int> _ambushWins;
        private static ConfigEntry<int> _ambushLosses;
        private static ConfigEntry<string> _lastMode;

        internal static void Initialize(ConfigFile config)
        {
            _wins = config.Bind("Record", "Wins", 0, "Completed PvP victories.");
            _losses = config.Bind("Record", "Losses", 0, "Completed PvP defeats.");
            _escapes = config.Bind("Record", "Escapes", 0, "PvP matches ending by player flight or attacker retreat/disengage.");
            _lastOpponent = config.Bind("Record", "LastOpponent", string.Empty, "Last completed PvP opponent.");
            _lastResult = config.Bind("Record", "LastResult", string.Empty, "Last completed PvP result.");
            _arrangedWins = config.Bind("Record", "ArrangedWins", 0, "Wins in accepted arranged PvP.");
            _arrangedLosses = config.Bind("Record", "ArrangedLosses", 0, "Losses in accepted arranged PvP.");
            _ambushWins = config.Bind("Record", "AmbushWins", 0, "Wild ambushes survived.");
            _ambushLosses = config.Bind("Record", "AmbushLosses", 0, "Defeats in wild ambushes.");
            _lastMode = config.Bind("Record", "LastMode", string.Empty, "Last completed PvP encounter mode.");
        }

        internal static void Complete(string opponent, string result, PvpEncounterMode mode)
        {
            if (result == "proxy_death") { _wins.Value++; if (mode == PvpEncounterMode.Ambush) _ambushWins.Value++; else _arrangedWins.Value++; }
            else if (result == "player_death") { _losses.Value++; if (mode == PvpEncounterMode.Ambush) _ambushLosses.Value++; else _arrangedLosses.Value++; }
            else if (result == "retreat" || result == "player_fled") _escapes.Value++;
            else return;
            _lastOpponent.Value = opponent ?? string.Empty;
            _lastResult.Value = result;
            _lastMode.Value = mode.ToString().ToLowerInvariant();
        }

        // Panel accessors. Reading through these keeps the UI safe before Initialize runs.
        internal static int Wins { get { return Read(_wins); } }
        internal static int Losses { get { return Read(_losses); } }
        internal static int Escapes { get { return Read(_escapes); } }
        internal static int ArrangedWins { get { return Read(_arrangedWins); } }
        internal static int ArrangedLosses { get { return Read(_arrangedLosses); } }
        internal static int AmbushWins { get { return Read(_ambushWins); } }
        internal static int AmbushLosses { get { return Read(_ambushLosses); } }
        internal static string LastOpponent { get { return Read(_lastOpponent); } }
        internal static string LastResult { get { return Read(_lastResult); } }
        internal static string LastMode { get { return Read(_lastMode); } }

        private static int Read(ConfigEntry<int> entry) { return entry == null ? 0 : entry.Value; }
        private static string Read(ConfigEntry<string> entry) { return entry == null ? string.Empty : entry.Value ?? string.Empty; }

        internal static string Describe()
        {
            return "record=" + _wins.Value + "W/" + _losses.Value + "L/" + _escapes.Value + " escaped; arranged=" + _arrangedWins.Value + "W/" + _arrangedLosses.Value + "L; ambush=" + _ambushWins.Value + "W/" + _ambushLosses.Value + "L";
        }
    }
}
