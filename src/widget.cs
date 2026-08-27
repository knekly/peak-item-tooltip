/**
 * @authors @katcw, @gintoki000 (for chinese simplified translations)
 * @file    widget.cs
 * @brief   builds and updates the ugui tooltip that shows an item's icon, name, type, description, and modifiers
 */
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;

namespace PeakItemTooltip
{
    /**
     * @brief the ugui tooltip that displays a hovered or held item's icon, name, type, description, and modifiers
     */
    public class Widget
    {
        private readonly PluginConfig _cfg;

        private GameObject _canvasGo;
        private GameObject _rootGo;
        private RectTransform _root;
        // rounded fill panel; a ProceduralImage (Image subclass) so .color/opacity still apply
        private ProceduralImage _background;
        // white outline overlay drawn on top of the fill, matching the game's status bar border
        private ProceduralImage _border;

        private GameObject _iconGo;
        private RawImage _icon;
        private LayoutElement _iconLayout;

        private TextMeshProUGUI _name;
        private TextMeshProUGUI _type;
        private TextMeshProUGUI _description;
        private TextMeshProUGUI _modifiers;

        // fallback type colour
        // used when an item has a type but that type isn't in the colour map
        private static readonly Color TypeFallbackColour = new Color(0.7f, 0.7f, 0.7f, 1f);

        // fixed rounded-panel style: corner radius (all four corners), white outline thickness, and its colour
        private const float CornerRadius = 12f;
        private const float BorderWidth = 2f;
        // fallback outline colour, used before ApplyConfig runs and when the configured hex fails to parse
        private static readonly Color DefaultBorderColour = Color.white;

        private bool _built;
        private bool _visible;

        // last BorderColour value we warned about, so a standing-invalid hex only logs once (ApplyConfig runs every show)
        private string _lastBadBorderColour;

        /**
         * @brief stores the config the widget reads its layout and field values from
         * @param cfg the plugin config supplying the widget's layout and field settings
         */
        public Widget(PluginConfig cfg)
        {
            _cfg = cfg;
        }

        public bool Visible => _visible;

        /**
         * @brief fills the widget with the item's icon, name, type, description, and modifiers, then makes it visible
         * @param item the item to describe
         * @param descriptions the store supplying the item's description and type colour
         */
        public void Show(Item item, Descriptions descriptions)
        {
            if (!_cfg.Enabled.Value)
            {
                Hide();
                return;
            }

            EnsureBuilt();

            // only show a tooltip for items we have an authored entry for; unlisted items (e.g. airport-only
            // props like chess pieces) have no key in the descriptions file and get no tooltip at all
            string key = item.UIData != null ? item.UIData.itemName : null;
            if (!descriptions.TryGet(key, out ItemInfo info))
            {
                Hide();
                return;
            }

            // icon
            Texture2D tex = null;
            try { tex = item.UIData != null ? item.UIData.GetIcon() : null; }
            catch { tex = null; }
            _icon.texture = tex;

            // name
            _name.text = item.GetItemName();

            _description.text = info.Description;

            // type(s): each type name is coloured from the shared type->colour map via rich-text tags
            _type.text = FormatTypes(info.Types, descriptions);

            // modifiers are derived live from the item's Action_ModifyStatus components
            _modifiers.text = ItemModifiers.Format(item);

            ApplyConfig();
            SetVisible(true);
        }

        /**
         * @brief hides the widget if it has been built
         */
        public void Hide()
        {
            if (!_built)
            {
                _visible = false;
                return;
            }
            SetVisible(false);
        }

        /**
         * @brief pushes the current config values into the widget's layout, sizes, colours, and field visibility
         */
        public void ApplyConfig()
        {
            if (!_built) return;

            _root.anchoredPosition = new Vector2(_cfg.OffsetX.Value, _cfg.OffsetY.Value);

            float s = _cfg.Scale.Value;
            _root.localScale = new Vector3(s, s, 1f);
            _root.sizeDelta = new Vector2(_cfg.Width.Value, _root.sizeDelta.y);

            Color bg = _background.color;
            bg.a = _cfg.BackgroundOpacity.Value;
            _background.color = bg;

            // outline: RGB from the BorderColour hex (falling back to white on a bad value), alpha from BorderOpacity
            Color borderCol;
            if (ColorUtility.TryParseHtmlString(_cfg.BorderColour.Value, out Color bc)) borderCol = bc;
            else
            {
                borderCol = DefaultBorderColour;
                WarnBadBorderColour(_cfg.BorderColour.Value);
            }
            borderCol.a = _cfg.BorderOpacity.Value;
            _border.color = borderCol;

            // hide icon where there is no texture
            _iconGo.SetActive(_cfg.ShowIcon.Value && _icon.texture != null);
            _name.gameObject.SetActive(_cfg.ShowName.Value);
            // type block hides when disabled or when the item has no type set
            _type.gameObject.SetActive(_cfg.ShowType.Value && !string.IsNullOrEmpty(_type.text));
            _description.gameObject.SetActive(_cfg.ShowDescription.Value);
            // modifier block hides when disabled or when the item has no modifiers
            _modifiers.gameObject.SetActive(_cfg.ShowModifiers.Value && !string.IsNullOrEmpty(_modifiers.text));

            _iconLayout.preferredWidth = _cfg.IconSize.Value;
            _iconLayout.preferredHeight = _cfg.IconSize.Value;
            _name.fontSize = _cfg.NameFontSize.Value;
            _type.fontSize = _cfg.TypeFontSize.Value;
            _description.fontSize = _cfg.DescriptionFontSize.Value;
            _modifiers.fontSize = _cfg.ModifierFontSize.Value;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_root);
        }

        /**
         * @brief logs a warning about an unparseable BorderColour value
         * @param value the raw BorderColour string that failed to parse
         */
        private void WarnBadBorderColour(string value)
        {
            if (value == _lastBadBorderColour) return;
            _lastBadBorderColour = value;
            Plugin.Log.LogWarning($"Could not parse BorderColour '{value}'; expected a hex colour like #FFFFFF. Falling back to white.");
        }

        /**
         * @brief toggles the root object's active state and records the visibility
         * @param v true to show the widget, false to hide it
         */
        private void SetVisible(bool v)
        {
            _visible = v;
            if (_rootGo != null) _rootGo.SetActive(v);
        }

        /**
         * @brief builds the canvas and all widget elements once, on first show, when the game font is available
         */
        private void EnsureBuilt()
        {
            if (_built)
                return;

            // canvas, screen space overlay
            _canvasGo = new GameObject("PeakItemTooltipCanvas");
            UnityEngine.Object.DontDestroyOnLoad(_canvasGo);

            Canvas canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000; // draw on top of the game hud

            CanvasScaler scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // root panel
            _rootGo = new GameObject("Widget");
            _rootGo.transform.SetParent(_canvasGo.transform, false);
            _root = _rootGo.AddComponent<RectTransform>();
            _root.anchorMin = new Vector2(0.5f, 0.5f);
            _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);

            // rounded black fill: a ProceduralImage renders the rounded rect via the game's shader
            _background = _rootGo.AddComponent<ProceduralImage>();
            _background.color = new Color(0f, 0f, 0f, _cfg.BackgroundOpacity.Value);
            _background.raycastTarget = false;
            // FreeModifier holds the per-corner radius
            FreeModifier bgRadius = _rootGo.AddComponent<FreeModifier>();
            bgRadius.Radius = new Vector4(CornerRadius, CornerRadius, CornerRadius, CornerRadius);

            VerticalLayoutGroup vlg = _rootGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 10, 10);
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperLeft;

            ContentSizeFitter fitter = _rootGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; // width is fixed via config
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;   // height fits content

            // header row: icon with name beside 
            GameObject headerGo = new GameObject("Header");
            headerGo.transform.SetParent(_root, false);
            headerGo.AddComponent<RectTransform>();

            HorizontalLayoutGroup hlg = headerGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            _iconGo = new GameObject("Icon");
            _iconGo.transform.SetParent(headerGo.transform, false);
            _iconGo.AddComponent<RectTransform>();
            _icon = _iconGo.AddComponent<RawImage>();
            _icon.raycastTarget = false;
            _iconLayout = _iconGo.AddComponent<LayoutElement>();
            _iconLayout.preferredWidth = _cfg.IconSize.Value;
            _iconLayout.preferredHeight = _cfg.IconSize.Value;

            TMP_FontAsset font = GetGameFont();

            _name = MakeText("Name", headerGo.transform, font, _cfg.NameFontSize.Value, FontStyles.Normal, Color.white);
            LayoutElement nameLE = _name.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1f; // name fills the rest of the header row

            // type sits between the name and the description; colour is set per-item in Show
            _type = MakeText("Type", _root, font, _cfg.TypeFontSize.Value, FontStyles.Italic, TypeFallbackColour);
            _type.textWrappingMode = TextWrappingModes.Normal;

            _description = MakeText("Description", _root, font, _cfg.DescriptionFontSize.Value, FontStyles.Normal, new Color(0.85f, 0.85f, 0.85f, 1f));
            _description.textWrappingMode = TextWrappingModes.Normal;

            // modifier block: per-status colours come from rich-text tags, so base colour is white
            _modifiers = MakeText("Modifiers", _root, font, _cfg.ModifierFontSize.Value, FontStyles.Normal, Color.white);
            _modifiers.textWrappingMode = TextWrappingModes.Normal;
            _modifiers.richText = true;

            // white border overlay: added last so it draws on top of the fill and content. it is a
            // hollow rounded ring (BorderWidth > 0) stretched to cover the whole panel, ignored by the
            // layout group so it just tracks the root's size
            GameObject borderGo = new GameObject("Border");
            borderGo.transform.SetParent(_root, false);
            RectTransform borderRt = borderGo.AddComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;
            borderGo.AddComponent<LayoutElement>().ignoreLayout = true;

            _border = borderGo.AddComponent<ProceduralImage>();
            _border.color = DefaultBorderColour;
            _border.BorderWidth = BorderWidth;
            _border.raycastTarget = false;
            FreeModifier borderRadius = borderGo.AddComponent<FreeModifier>();
            borderRadius.Radius = new Vector4(CornerRadius, CornerRadius, CornerRadius, CornerRadius);

            _built = true;
            SetVisible(false);
        }

        /**
         * @brief builds a rich-text string of the item's type(s), each name wrapped in its own colour tag and
         *        separated by a slash, so an item can display multiple types at once
         * @param types the item's type names
         * @param descriptions the store supplying each type's colour
         * @return the coloured, slash-separated type string, or empty when there are no types
         */
        private static string FormatTypes(List<string> types, Descriptions descriptions)
        {
            if (types == null) return "";

            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (string t in types)
            {
                if (string.IsNullOrEmpty(t)) continue;

                Color c = descriptions.TryGetTypeColour(t, out Color tc) ? tc : TypeFallbackColour;
                if (!first) sb.Append(" / ");
                first = false;

                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(c)).Append('>')
                  .Append(TypeName(t)).Append("</color>");
            }

            return sb.ToString();
        }

        /**
         * @brief localised display name for an item type (zh-CN patch)
         */
        private static string TypeName(string t)
        {
            if (!Zh.IsActive()) return t;
            switch (t)
            {
                case "MYSTICAL":   return "神秘";
                case "CONSUMABLE": return "消耗品";
                case "DEPLOYABLE": return "可部署";
                case "UNUSED":     return "未使用";
                case "EQUIPMENT":  return "装备";
                case "MISC":       return "杂物";
                default: return t;
            }
        }

        /**
         * @brief creates a text element under the given parent with the supplied font, size, style, and colour
         * @param name the object name for the new text element
         * @param parent the transform to attach the text element to
         * @param font the font to apply, or null to keep the tmp default
         * @param size the font size
         * @param style the font style
         * @param colour the text colour
         * @return the created text component
         */
        private static TextMeshProUGUI MakeText(string name, Transform parent, TMP_FontAsset font,
            float size, FontStyles style, Color colour)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = colour;
            t.raycastTarget = false;
            return t;
        }

        /**
         * @brief returns the game's hud font so the tooltip text matches it, or null if it is not available yet
         */
        private static TMP_FontAsset GetGameFont()
        {
            if (GUIManager.instance != null && GUIManager.instance.interactNameText != null) return GUIManager.instance.interactNameText.font;
            return null;
        }
    }
}
