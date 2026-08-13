using System;

namespace ErenshorPvP
{
    // Unity-free retained-uGUI geometry. Positions are normalized from the bottom-left corner.
    // Any old IMGUI pixel value (> 1) is intentionally rejected rather than mirrored into the
    // bottom-left coordinate system.
    internal struct PvpUiRect
    {
        internal float X;
        internal float Y;
        internal float Width;
        internal float Height;

        internal PvpUiRect(float x, float y, float width, float height)
        {
            X = x; Y = y; Width = width; Height = height;
        }
    }

    internal static class PvpUiGeometry
    {
        internal const float Unset = -1f;
        internal const float LauncherWidth = 148f;
        internal const float LauncherHeight = 32f;
        internal const float PanelWidth = 470f;
        internal const float PanelHeight = 520f;
        internal const float Margin = 10f;

        internal static float InterpretStoredAxis(float stored)
        {
            if (!Finite(stored) || stored < 0f || stored > 1f) return Unset;
            return stored;
        }

        internal static float NormalizeAxis(float pixels, float screenExtent)
        {
            if (!Finite(pixels) || !Finite(screenExtent) || screenExtent <= 0f) return 0f;
            return Clamp(pixels / screenExtent, 0f, 1f);
        }

        internal static PvpUiRect ResolvePanel(float storedX, float storedY, float screenWidth, float screenHeight)
        {
            float x = InterpretStoredAxis(storedX);
            float y = InterpretStoredAxis(storedY);
            if (x == Unset || y == Unset)
            {
                float defaultX = Math.Max(Margin, screenWidth - PanelWidth - 22f);
                // Roughly preserves the old "below minimap" placement without migrating its
                // top-origin persisted offsets.
                float defaultY = Math.Max(Margin, screenHeight - PanelHeight - 195f);
                return ClampPanel(new PvpUiRect(defaultX, defaultY, PanelWidth, PanelHeight), screenWidth, screenHeight);
            }
            return ClampPanel(new PvpUiRect(x * screenWidth, y * screenHeight, PanelWidth, PanelHeight), screenWidth, screenHeight);
        }

        internal static PvpUiRect ResolveLauncher(float storedX, float storedY, float screenWidth, float screenHeight)
        {
            float x = InterpretStoredAxis(storedX);
            float y = InterpretStoredAxis(storedY);
            if (x == Unset || y == Unset)
            {
                float defaultX = Math.Max(Margin, screenWidth - LauncherWidth - 22f);
                float defaultY = Math.Max(Margin, screenHeight - LauncherHeight - 172f);
                return ClampLauncher(new PvpUiRect(defaultX, defaultY, LauncherWidth, LauncherHeight), screenWidth, screenHeight);
            }
            return ClampLauncher(new PvpUiRect(x * screenWidth, y * screenHeight, LauncherWidth, LauncherHeight), screenWidth, screenHeight);
        }

        internal static PvpUiRect ClampPanel(PvpUiRect r, float screenWidth, float screenHeight)
        {
            r.Width = Math.Min(PanelWidth, Math.Max(240f, screenWidth - (Margin * 2f)));
            r.Height = Math.Min(PanelHeight, Math.Max(240f, screenHeight - (Margin * 2f)));
            r.X = Clamp(Finite(r.X) ? r.X : Margin, Margin, Math.Max(Margin, screenWidth - r.Width - Margin));
            r.Y = Clamp(Finite(r.Y) ? r.Y : Margin, Margin, Math.Max(Margin, screenHeight - r.Height - Margin));
            return r;
        }

        internal static PvpUiRect ClampLauncher(PvpUiRect r, float screenWidth, float screenHeight)
        {
            r.Width = LauncherWidth;
            r.Height = LauncherHeight;
            r.X = Clamp(Finite(r.X) ? r.X : Margin, Margin, Math.Max(Margin, screenWidth - r.Width - Margin));
            r.Y = Clamp(Finite(r.Y) ? r.Y : Margin, Margin, Math.Max(Margin, screenHeight - r.Height - Margin));
            return r;
        }

        internal static PvpUiRect ResolvePanel(float storedX, float storedY, float screenWidth, float screenHeight, float width, float height)
        {
            PvpUiRect r = ResolvePanel(storedX, storedY, screenWidth, screenHeight);
            r.Width = width; r.Height = height;
            return Clamp(r, screenWidth, screenHeight);
        }

        internal static PvpUiRect ResolveLauncher(float storedX, float storedY, float screenWidth, float screenHeight, float width, float height)
        {
            PvpUiRect r = ResolveLauncher(storedX, storedY, screenWidth, screenHeight);
            r.Width = width; r.Height = height;
            return Clamp(r, screenWidth, screenHeight);
        }

        internal static PvpUiRect Clamp(PvpUiRect r, float screenWidth, float screenHeight)
        {
            r.Width = Math.Min(r.Width, Math.Max(80f, screenWidth - (Margin * 2f)));
            r.Height = Math.Min(r.Height, Math.Max(32f, screenHeight - (Margin * 2f)));
            r.X = Clamp(Finite(r.X) ? r.X : Margin, Margin, Math.Max(Margin, screenWidth - r.Width - Margin));
            r.Y = Clamp(Finite(r.Y) ? r.Y : Margin, Margin, Math.Max(Margin, screenHeight - r.Height - Margin));
            return r;
        }

        internal static void Normalize(PvpUiRect r, float screenWidth, float screenHeight, out float x, out float y)
        {
            x = NormalizeAxis(r.X, screenWidth);
            y = NormalizeAxis(r.Y, screenHeight);
        }

        internal static string RunSelfTests()
        {
            if (InterpretStoredAxis(float.NaN) != Unset) return "FAIL pvp ui NaN storage";
            if (InterpretStoredAxis(250f) != Unset) return "FAIL pvp ui legacy pixel rejection";
            if (Math.Abs(InterpretStoredAxis(0.5f) - 0.5f) > 0.0001f) return "FAIL pvp ui normalized storage";

            PvpUiRect panel = ResolvePanel(Unset, Unset, 1920f, 1080f);
            if (panel.X < Margin || panel.Y < Margin || panel.X + panel.Width > 1920f || panel.Y + panel.Height > 1080f)
                return "FAIL pvp ui default clamp";

            float nx = NormalizeAxis(500f, 1920f);
            float ny = NormalizeAxis(250f, 1080f);
            PvpUiRect restored = ResolvePanel(nx, ny, 1920f, 1080f);
            if (Math.Abs(restored.X - 500f) > 0.1f || Math.Abs(restored.Y - 250f) > 0.1f)
                return "FAIL pvp ui normalized round trip";

            PvpUiRect tiny = ResolvePanel(1f, 1f, 640f, 360f);
            if (tiny.X < 0f || tiny.Y < 0f || tiny.X + tiny.Width > 640.1f || tiny.Y + tiny.Height > 360.1f)
                return "FAIL pvp ui small screen recovery";
            return "PASS pvp retained ui geometry";
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
