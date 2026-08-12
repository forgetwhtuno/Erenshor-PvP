namespace ErenshorPvP
{
    internal static class PvpRecordService
    {
        private static PvpConfigEntry<int> _wins;
        private static PvpConfigEntry<int> _losses;
        private static PvpConfigEntry<int> _escapes;
        private static PvpConfigEntry<string> _lastOpponent;
        private static PvpConfigEntry<string> _lastResult;
        private static PvpConfigEntry<int> _arrangedWins;
        private static PvpConfigEntry<int> _arrangedLosses;
        private static PvpConfigEntry<int> _ambushWins;
        private static PvpConfigEntry<int> _ambushLosses;
        private static PvpConfigEntry<string> _lastMode;

        internal static void Initialize(PvpSettings settings)
        {
            _wins = new PvpConfigEntry<int>(() => settings.RecordWins, v => settings.RecordWins = v);
            _losses = new PvpConfigEntry<int>(() => settings.RecordLosses, v => settings.RecordLosses = v);
            _escapes = new PvpConfigEntry<int>(() => settings.RecordEscapes, v => settings.RecordEscapes = v);
            _lastOpponent = new PvpConfigEntry<string>(() => settings.LastOpponent, v => settings.LastOpponent = v);
            _lastResult = new PvpConfigEntry<string>(() => settings.LastResult, v => settings.LastResult = v);
            _arrangedWins = new PvpConfigEntry<int>(() => settings.ArrangedWins, v => settings.ArrangedWins = v);
            _arrangedLosses = new PvpConfigEntry<int>(() => settings.ArrangedLosses, v => settings.ArrangedLosses = v);
            _ambushWins = new PvpConfigEntry<int>(() => settings.AmbushWins, v => settings.AmbushWins = v);
            _ambushLosses = new PvpConfigEntry<int>(() => settings.AmbushLosses, v => settings.AmbushLosses = v);
            _lastMode = new PvpConfigEntry<string>(() => settings.LastMode, v => settings.LastMode = v);
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
            PvpController.PersistSettings();
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

        private static int Read(PvpConfigEntry<int> entry) { return entry == null ? 0 : entry.Value; }
        private static string Read(PvpConfigEntry<string> entry) { return entry == null ? string.Empty : entry.Value ?? string.Empty; }

        internal static string Describe()
        {
            return "record=" + _wins.Value + "W/" + _losses.Value + "L/" + _escapes.Value + " escaped; arranged=" + _arrangedWins.Value + "W/" + _arrangedLosses.Value + "L; ambush=" + _ambushWins.Value + "W/" + _ambushLosses.Value + "L";
        }
    }
}
