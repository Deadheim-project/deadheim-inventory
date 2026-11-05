using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadheim.InventoryUI
{
    internal static class ValheimUi
    {
        internal static readonly Color Orange = new Color(1f, 0.63f, 0.24f, 1f);
        internal static readonly Color Beige = new Color(0.85f, 0.73f, 0.53f, 1f);
        internal static readonly Color Muted = new Color(0.62f, 0.58f, 0.50f, 1f);
        internal static readonly Color Dark = new Color(0.045f, 0.035f, 0.025f, 0.95f);
        internal static readonly Color Green = new Color(0.55f, 0.9f, 0.42f, 1f);

        private static TMP_FontAsset _bodyFont;
        private static TMP_FontAsset _displayFont;
        private static Sprite _panelSprite;
        private static Sprite _buttonSprite;
        private static Sprite _fieldSprite;
        private static bool _ready;
        private static float _nextAssetTry;

        internal static Sprite PanelSprite => _panelSprite;
        internal static Sprite FieldSprite => _fieldSprite;

        internal static bool EnsureAssets()
        {
            if (_ready) return true;
            // Falhou há pouco: não varrer todos os sprites/fontes de novo a cada frame.
            if (Time.unscaledTime < _nextAssetTry) return false;
            _nextAssetTry = Time.unscaledTime + 3f;
            _bodyFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                .FirstOrDefault(font => font != null && font.name == "Valheim-AveriaSansLibre");
            _displayFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                .FirstOrDefault(font => font != null && font.name == "Valheim-Norse") ?? _bodyFont;
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            _panelSprite = sprites.FirstOrDefault(sprite => sprite.name == "woodpanel_trophys")
                ?? sprites.FirstOrDefault(sprite => sprite.name == "woodpanel_settings");
            _buttonSprite = sprites.FirstOrDefault(sprite => sprite.name == "button");
            _fieldSprite = sprites.FirstOrDefault(sprite => sprite.name == "text_field");
            if (_bodyFont == null || _panelSprite == null) return false;
            _ready = true;
            return true;
        }

        internal static RectTransform Rect(string name, Transform parent, bool active = true)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            if (!active) go.SetActive(false);
            return (RectTransform)go.transform;
        }

        internal static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f,
            float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        internal static void Anchor(RectTransform rect, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        internal static RectTransform CreatePanel(Transform parent, string name, bool sliced = true)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = _panelSprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.color = Color.white;
            return rect;
        }

        internal static TextMeshProUGUI Label(Transform parent, string text, int size = 16,
            Color? color = null, TextAlignmentOptions alignment = TextAlignmentOptions.Left, bool display = false)
        {
            var rect = Rect("Text", parent, active: false);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = display ? _displayFont : _bodyFont;
            label.fontSize = size;
            label.color = color ?? Beige;
            label.alignment = alignment;
            label.text = text ?? string.Empty;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            rect.gameObject.SetActive(true);
            return label;
        }

        internal static Button Button(Transform parent, string title, int size = 15)
        {
            var rect = Rect("Button - " + title, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = _buttonSprite ?? _fieldSprite ?? _panelSprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, 0.96f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1.22f, 1.15f, 0.95f, 1f),
                pressedColor = new Color(0.78f, 0.69f, 0.53f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.5f, 0.48f, 0.43f, 0.55f),
                colorMultiplier = 1f,
                fadeDuration = Plugin.ReduceMotion != null && Plugin.ReduceMotion.Value ? 0f : 0.12f
            };
            var label = Label(rect, title, size, Orange, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform, 6f, 2f, 6f, 2f);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 40f;
            layout.flexibleHeight = 0f;
            return button;
        }

        internal static RectTransform ScrollList(Transform parent, float spacing = 5f)
        {
            var viewport = Rect("Viewport", parent);
            Stretch(viewport, 2f, 2f, 16f, 2f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            return content;
        }

        internal static void FixedHeight(GameObject go, float height)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0f;
        }

        internal static void FixedWidth(GameObject go, float width)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.flexibleWidth = 0f;
        }

        private static GameObject _tooltipPrefab;

        // Reaproveita o tooltip nativo do jogo (mesmo visual do inventário). Sem prefab, não faz nada.
        internal static void AddTooltip(GameObject go, string topic, string text)
        {
            if (go == null || string.IsNullOrEmpty(text)) return;
            if (_tooltipPrefab == null)
                _tooltipPrefab = Resources.FindObjectsOfTypeAll<UITooltip>()
                    .Select(tooltip => tooltip.m_tooltipPrefab)
                    .FirstOrDefault(prefab => prefab != null);
            if (_tooltipPrefab == null) return;
            var tooltip = go.GetComponent<UITooltip>() ?? go.AddComponent<UITooltip>();
            tooltip.m_tooltipPrefab = _tooltipPrefab;
            tooltip.m_topic = topic ?? string.Empty;
            tooltip.m_text = text;
        }

        internal static Sprite ItemIcon(string prefabName)
        {
            if (ObjectDbAccess(prefabName, out var drop) && drop.m_itemData?.m_shared?.m_icons != null
                && drop.m_itemData.m_shared.m_icons.Length > 0)
                return drop.m_itemData.m_shared.m_icons[0];
            return null;
        }

        private static bool ObjectDbAccess(string prefabName, out ItemDrop drop)
        {
            drop = null;
            if (ObjectDB.instance == null || string.IsNullOrEmpty(prefabName)) return false;
            var prefab = ObjectDB.instance.GetItemPrefab(prefabName);
            if (prefab == null) return false;
            drop = prefab.GetComponent<ItemDrop>();
            return drop != null;
        }
    }
}
