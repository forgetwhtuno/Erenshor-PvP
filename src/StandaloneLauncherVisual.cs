using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ErenshorPvP
{
    // Canonical Forgotten Roads standalone-launcher chrome. This file is intentionally copied
    // source, not shared as a runtime assembly, so every module remains independently loadable.
    internal static class StandaloneLauncherVisual
    {
        internal const float Width = 154f;
        internal const float Height = 32f;
        internal const float GripWidth = 20f;
        internal const float Border = 1f;
        internal const float LabelFontSize = 11f;

        internal static readonly Color Background = new Color(0.015f, 0.09f, 0.125f, 0.72f);
        internal static readonly Color GripBackground = new Color(0.025f, 0.13f, 0.17f, 0.88f);
        internal static readonly Color Control = new Color(0.035f, 0.17f, 0.22f, 0.78f);
        internal static readonly Color Hover = new Color(0.12f, 0.38f, 0.48f, 0.90f);
        internal static readonly Color Pressed = new Color(0.08f, 0.28f, 0.36f, 0.94f);
        internal static readonly Color Cyan = new Color(0.03f, 0.67f, 0.86f, 0.95f);
        internal static readonly Color Text = new Color(0.88f, 0.92f, 0.91f, 1f);

        internal static void StyleRoot(RectTransform root)
        {
            if (root == null) return;
            root.sizeDelta = new Vector2(Width, Height);
            Image image = root.GetComponent<Image>();
            if (image == null) image = root.gameObject.AddComponent<Image>();
            image.color = Background;
            image.raycastTarget = true;
            AddFrame(root);
        }

        internal static void StyleGrip(RectTransform grip)
        {
            if (grip == null) return;
            grip.sizeDelta = new Vector2(GripWidth, Height);
            Image image = grip.GetComponent<Image>();
            if (image == null) image = grip.gameObject.AddComponent<Image>();
            image.color = GripBackground;
            image.raycastTarget = true;

            AddBlock("GripAccent", grip, new Vector2(2f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0f));
            for (int i = -1; i <= 1; i++)
                AddBlock("GripDot", grip, new Vector2(2f, 2f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(1f, i * 5f));
        }

        internal static void StyleButton(Button button, TextMeshProUGUI label)
        {
            if (button == null || button.targetGraphic == null) return;
            ColorBlock colors = button.colors;
            colors.normalColor = Control;
            colors.highlightedColor = Hover;
            colors.pressedColor = Pressed;
            colors.selectedColor = Hover;
            colors.disabledColor = new Color(0.03f, 0.10f, 0.13f, 0.58f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.targetGraphic.CrossFadeColor(Control, 0f, true, true);
            if (label != null)
            {
                label.fontSize = LabelFontSize;
                label.fontStyle = FontStyles.Normal;
                label.color = Text;
                label.alignment = TextAlignmentOptions.Center;
                label.enableWordWrapping = false;
            }
        }

        internal static RectTransform AddVerticalChevron(RectTransform parent, bool pointsUp)
        {
            RectTransform icon = AddRect("Chevron", parent, new Vector2(12f, 10f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
            AddBar(icon, new Vector2(-2.2f, pointsUp ? -1f : 1f), pointsUp ? -45f : 45f);
            AddBar(icon, new Vector2(2.2f, pointsUp ? -1f : 1f), pointsUp ? 45f : -45f);
            return icon;
        }

        private static void AddFrame(RectTransform parent)
        {
            AddBlock("FrameTop", parent, new Vector2(0f, Border), new Vector2(0f, 1f), new Vector2(1f, 1f));
            AddBlock("FrameBottom", parent, new Vector2(0f, Border), new Vector2(0f, 0f), new Vector2(1f, 0f));
            AddBlock("FrameLeft", parent, new Vector2(Border, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f));
            AddBlock("FrameRight", parent, new Vector2(Border, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f));
        }

        private static void AddBar(RectTransform parent, Vector2 position, float rotation)
        {
            RectTransform bar = AddBlock("ChevronBar", parent, new Vector2(2f, 7f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position);
            bar.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private static RectTransform AddBlock(string name, Transform parent, Vector2 size,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            return AddBlock(name, parent, size, anchorMin, anchorMax, Vector2.zero);
        }

        private static RectTransform AddBlock(string name, Transform parent, Vector2 size,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Image image = go.GetComponent<Image>();
            image.color = Cyan;
            image.raycastTarget = false;
            return rect;
        }

        private static RectTransform AddRect(string name, Transform parent, Vector2 size,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
    }
}
