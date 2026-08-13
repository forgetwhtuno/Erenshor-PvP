using UnityEngine;

namespace ErenshorPvP
{
    // Compact GUI.Window-based launcher, matching the Journal/Contracts/Guild Life suite
    // convention. A GUI.Window always renders after (in front of) any raw, non-window IMGUI
    // control drawn that frame across every active mod's OnGUI, regardless of call order or
    // GUI.depth -- so the previous bare GUI.Toggle quick-switch could be silently painted over
    // by any other mod's window sharing the same screen region. Participating in the same
    // deferred window-rendering pass fixes that structurally.
    internal sealed class PvpLauncher
    {
        private const int WindowId = 0x45525056;
        internal const float Width = 118f;
        internal const float Height = 34f;

        private bool _requestToggle;
        private bool _open;
        private bool _enabled;
        private Texture2D _panelTexture;
        private Texture2D _buttonTexture;
        private Texture2D _buttonHoverTexture;
        private Texture2D _buttonOpenTexture;
        private GUIStyle _windowStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _openButtonStyle;
        private GUIStyle _gripStyle;

        internal bool RequestToggle
        {
            get { return _requestToggle; }
        }

        internal Rect Draw(Rect rect, bool open, bool enabled)
        {
            EnsureStyles();
            _open = open;
            _enabled = enabled;
            _requestToggle = false;
            int previousDepth = GUI.depth;
            Rect result;
            try
            {
                // More negative than PvpPanel's -45 so the launcher stays visible/clickable
                // on top of the full panel too, matching Journal's launcher/window ordering.
                GUI.depth = -50;
                result = GUI.Window(WindowId, rect, DrawContents, GUIContent.none, _windowStyle);
            }
            finally { GUI.depth = previousDepth; }
            return result;
        }

        internal void Dispose()
        {
            Destroy(ref _panelTexture);
            Destroy(ref _buttonTexture);
            Destroy(ref _buttonHoverTexture);
            Destroy(ref _buttonOpenTexture);
            _windowStyle = null;
            _buttonStyle = null;
            _openButtonStyle = null;
            _gripStyle = null;
        }

        // A narrow grip owns dragging, matching Journal's launcher. The action button no longer
        // sits under GUI.DragWindow's rect -- overlapping the two meant a click could be consumed
        // as a drag-start instead of a button press, live-confirmed as unreliable open/close and
        // unreliable dragging on this launcher (Journal, whose drag rect never overlaps its button,
        // did not have this problem).
        private void DrawContents(int id)
        {
            GUI.Label(new Rect(3f, 5f, 14f, Height - 10f), "||", _gripStyle);
            string label = "PVP " + (_enabled ? "ON" : "OFF");
            GUIStyle style = _open ? _openButtonStyle : _buttonStyle;
            Color previous = GUI.color;
            GUI.color = _enabled ? new Color(0.65f, 1f, 0.75f, 1f) : new Color(0.85f, 0.85f, 0.85f, 1f);
            if (GUI.Button(new Rect(18f, 4f, Width - 22f, Height - 8f), label, style))
                _requestToggle = true;
            GUI.color = previous;
            GUI.DragWindow(new Rect(0f, 0f, 18f, Height));
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null) return;
            Color cyan = new Color(0.03f, 0.67f, 0.86f, 0.95f);
            Color soft = new Color(0.13f, 0.55f, 0.68f, 0.90f);
            _panelTexture = Framed(new Color(0.015f, 0.09f, 0.125f, 0.74f), cyan);
            _buttonTexture = Framed(new Color(0.035f, 0.17f, 0.22f, 0.88f), soft);
            _buttonHoverTexture = Framed(new Color(0.12f, 0.38f, 0.48f, 0.94f), cyan);
            _buttonOpenTexture = Framed(new Color(0.08f, 0.30f, 0.36f, 0.96f), cyan);

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _panelTexture;
            _windowStyle.border = new RectOffset(1, 1, 1, 1);
            _windowStyle.padding = new RectOffset(0, 0, 0, 0);

            _buttonStyle = Button(_buttonTexture, _buttonHoverTexture);
            _openButtonStyle = Button(_buttonOpenTexture, _buttonHoverTexture);
            _openButtonStyle.fontStyle = FontStyle.Bold;

            _gripStyle = new GUIStyle(GUI.skin.label);
            _gripStyle.fontSize = 10;
            _gripStyle.fontStyle = FontStyle.Bold;
            _gripStyle.alignment = TextAnchor.MiddleCenter;
            _gripStyle.normal.textColor = new Color(0.56f, 0.88f, 1f, 0.95f);
        }

        private static GUIStyle Button(Texture2D normal, Texture2D hover)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = hover;
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.fontSize = 11;
            style.border = new RectOffset(1, 1, 1, 1);
            return style;
        }

        private static Texture2D Framed(Color center, Color edge)
        {
            Texture2D texture = new Texture2D(3, 3, TextureFormat.RGBA32, false);
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                    texture.SetPixel(x, y, x == 0 || x == 2 || y == 0 || y == 2 ? edge : center);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            texture.Apply(false, true);
            return texture;
        }

        private static void Destroy(ref Texture2D texture)
        {
            if (texture == null) return;
            Object.Destroy(texture);
            texture = null;
        }
    }
}
