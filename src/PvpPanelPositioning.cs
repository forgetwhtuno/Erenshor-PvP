using System;

namespace ErenshorPvP
{
    // Mirrors the Party Tools panel placement contract so both mods anchor to the same
    // upper-right area below the minimap and never overlap the character/party panels.
    internal struct PvpPanelPosition
    {
        internal readonly float X;
        internal readonly float Y;

        internal PvpPanelPosition(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    internal struct PvpPanelOffsets
    {
        internal readonly float X;
        internal readonly float Y;

        internal PvpPanelOffsets(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    internal static class PvpPanelPositioning
    {
        internal const float ScreenMargin = 8f;
        internal const float RightMargin = 18f;
        internal const float DefaultTop = 336f;
        private const float PositionEpsilon = 0.01f;

        internal static PvpPanelPosition Resolve(
            float screenWidth,
            float screenHeight,
            float panelWidth,
            float panelHeight,
            float offsetX,
            float offsetY)
        {
            offsetX = FiniteOrDefault(offsetX, 0f);
            offsetY = FiniteOrDefault(offsetY, 0f);
            float desiredX = screenWidth - panelWidth - RightMargin - offsetX;
            float desiredY = DefaultTop + offsetY;
            return Clamp(screenWidth, screenHeight, panelWidth, panelHeight, desiredX, desiredY);
        }

        internal static PvpPanelPosition Clamp(
            float screenWidth,
            float screenHeight,
            float panelWidth,
            float panelHeight,
            float desiredX,
            float desiredY)
        {
            float maxX = Math.Max(ScreenMargin, screenWidth - panelWidth - ScreenMargin);
            float maxY = Math.Max(ScreenMargin, screenHeight - panelHeight - ScreenMargin);
            return new PvpPanelPosition(
                ClampValue(desiredX, ScreenMargin, maxX),
                ClampValue(desiredY, ScreenMargin, maxY));
        }

        internal static PvpPanelOffsets ToOffsets(float screenWidth, float panelWidth, PvpPanelPosition position)
        {
            return new PvpPanelOffsets(
                screenWidth - panelWidth - RightMargin - position.X,
                position.Y - DefaultTop);
        }

        internal static bool NearlyEqual(float left, float right)
        {
            return Math.Abs(left - right) <= PositionEpsilon;
        }

        private static float ClampValue(float value, float minimum, float maximum)
        {
            if (!IsFinite(value)) return minimum;
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

        private static float FiniteOrDefault(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static string RunSelfTests()
        {
            PvpPanelPosition resolved = Resolve(1920f, 1080f, 330f, 400f, 0f, 0f);
            if (!NearlyEqual(resolved.X, 1920f - 330f - RightMargin)) return "FAIL default x";
            if (!NearlyEqual(resolved.Y, DefaultTop)) return "FAIL default y";

            // A panel taller than the screen still lands inside the visible area.
            PvpPanelPosition tall = Resolve(800f, 300f, 330f, 900f, 0f, 0f);
            if (tall.Y < ScreenMargin) return "FAIL tall clamp";

            // Offsets round-trip so a dragged panel reopens where the player left it.
            PvpPanelPosition moved = Clamp(1920f, 1080f, 330f, 400f, 640f, 500f);
            PvpPanelOffsets offsets = ToOffsets(1920f, 330f, moved);
            PvpPanelPosition restored = Resolve(1920f, 1080f, 330f, 400f, offsets.X, offsets.Y);
            if (!NearlyEqual(restored.X, moved.X) || !NearlyEqual(restored.Y, moved.Y)) return "FAIL offset round trip";

            PvpPanelPosition garbage = Resolve(1920f, 1080f, 330f, 400f, float.NaN, float.PositiveInfinity);
            if (!NearlyEqual(garbage.X, 1920f - 330f - RightMargin)) return "FAIL non-finite offset recovery";
            return "PASS pvp panel positioning";
        }
    }

    internal sealed class PvpPanelPositionState
    {
        private readonly Action<float, float> _persist;
        private float _offsetX;
        private float _offsetY;
        private bool _dirty;

        internal PvpPanelPositionState(float offsetX, float offsetY, Action<float, float> persist)
        {
            _offsetX = offsetX;
            _offsetY = offsetY;
            _persist = persist;
        }

        internal float OffsetX { get { return _offsetX; } }
        internal float OffsetY { get { return _offsetY; } }

        internal PvpPanelPosition ResolveAndRecover(
            float screenWidth,
            float screenHeight,
            float panelWidth,
            float panelHeight)
        {
            PvpPanelPosition position = PvpPanelPositioning.Resolve(
                screenWidth, screenHeight, panelWidth, panelHeight, _offsetX, _offsetY);
            PvpPanelOffsets normalized = PvpPanelPositioning.ToOffsets(screenWidth, panelWidth, position);
            if (SetOffsets(normalized.X, normalized.Y))
            {
                _dirty = false;
                Persist();
            }
            return position;
        }

        internal PvpPanelPosition MoveTo(
            float screenWidth,
            float screenHeight,
            float panelWidth,
            float panelHeight,
            float desiredX,
            float desiredY)
        {
            PvpPanelPosition position = PvpPanelPositioning.Clamp(
                screenWidth, screenHeight, panelWidth, panelHeight, desiredX, desiredY);
            PvpPanelOffsets offsets = PvpPanelPositioning.ToOffsets(screenWidth, panelWidth, position);
            if (SetOffsets(offsets.X, offsets.Y)) _dirty = true;
            return position;
        }

        internal void Reset()
        {
            if (!SetOffsets(0f, 0f)) return;
            _dirty = false;
            Persist();
        }

        internal void CommitIfMoved()
        {
            if (!_dirty) return;
            _dirty = false;
            Persist();
        }

        private bool SetOffsets(float offsetX, float offsetY)
        {
            if (PvpPanelPositioning.NearlyEqual(_offsetX, offsetX) &&
                PvpPanelPositioning.NearlyEqual(_offsetY, offsetY))
                return false;

            _offsetX = offsetX;
            _offsetY = offsetY;
            return true;
        }

        private void Persist()
        {
            if (_persist != null) _persist(_offsetX, _offsetY);
        }
    }
}
