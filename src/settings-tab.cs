/**
 * @authors @katcw
 * @file    settings-tab.cs
 * @brief   adds an "Item Tooltip" tab to the game's own settings page (pause menu and main menu) that lists
 *          and edits every config entry of the mod, built from the same row and input prefabs as the game's tabs
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeakItemTooltip
{
    /**
     * @brief builds the tooltip tab's rows: each entry is a cloned SettingsUICell row holding a cloned dropdown
     *        or slider cell. the game scripts on the clones are stripped (they expect a game Setting object) and
     *        the controls are wired straight to the config entries. the rows sit in our own scroll view laid
     *        over the page's content area, since the game's tabs are short enough not to need one
     */
    public static class SettingsTab
    {
        // a SettingsCategory value past the game's own (General..Controls) that marks our tab
        internal const SettingsCategory Category = (SettingsCategory)1000;
        private const string TabName = "PeakItemTooltipTab";

        // set by the plugin on load
        internal static ConfigFile File;

        /**
         * @brief our scroll view and rows on one settings page (the pause menu and main menu each have one)
         */
        private class Page
        {
            public GameObject Scroll;
            public ScrollRect ScrollRect;
            public RectTransform Content;
            public readonly List<GameObject> Rows = new List<GameObject>();
        }

        private static readonly Dictionary<SharedSettingsMenu, Page> Pages = new Dictionary<SharedSettingsMenu, Page>();
        // our cloned tab buttons (pause menu and main menu), so their labels can follow the language setting
        private static readonly List<SettingsTABSButton> TabButtons = new List<SettingsTABSButton>();

        // input cell prefabs from the game's InputCellMapper asset. the game has no string cell, so text
        // values reuse the float cell's input box with its slider hidden
        private static GameObject _enumCell;
        private static GameObject _floatCell;
        private static GameObject _buttonCell;
        // one of the game's settings tab buttons, captured in AddTabButton
        private static GameObject _tabTemplate;

        // display names per "Section.Key" (en, zh); entries not listed fall back to their split-up key
        private static readonly Dictionary<string, string[]> Names = new Dictionary<string, string[]>
        {
            { "Localization.Language",       new[] { "Tooltip Language", "提示语言" } },
            { "Widget.Enabled",              new[] { "Show Item Tooltip", "显示物品提示" } },
            { "Widget.ShowWhileHolding",     new[] { "Show Tooltip For Held Item", "手持物品时显示提示" } },
            { "Widget.OffsetX",              new[] { "Tooltip Position X", "提示位置 X" } },
            { "Widget.OffsetY",              new[] { "Tooltip Position Y", "提示位置 Y" } },
            { "Widget.Scale",                new[] { "Tooltip Scale", "提示缩放" } },
            { "Widget.Width",                new[] { "Tooltip Width", "提示宽度" } },
            { "Widget.BackgroundOpacity",    new[] { "Background Opacity", "背景不透明度" } },
            { "Widget.BorderColour",         new[] { "Border Colour (Hex, e.g. #FFFFFF)", "边框颜色（十六进制，如 #FFFFFF）" } },
            { "Widget.BorderOpacity",        new[] { "Border Opacity", "边框不透明度" } },
            { "Fields.ShowIcon",             new[] { "Show Item Icon", "显示物品图标" } },
            { "Fields.ShowName",             new[] { "Show Item Name", "显示物品名称" } },
            { "Fields.ShowType",             new[] { "Show Item Type", "显示物品类型" } },
            { "Fields.ShowDescription",      new[] { "Show Item Description", "显示物品描述" } },
            { "Fields.ShowModifiers",        new[] { "Show Stat Modifiers", "显示属性变化" } },
            { "Fields.ShowStatusIcons",      new[] { "Use Status Icons For Modifiers", "属性变化使用状态图标" } },
            { "Fields.ShowUses",           new[] { "Show Remaining Uses", "显示剩余次数" } },
            { "Fields.ShowUsesWhileHolding", new[] { "Show Remaining Uses For Held Item", "手持时显示剩余次数" } },
            { "Fields.ShowWeight",           new[] { "Show Item Weight", "显示物品重量" } },
            { "Fields.IconSize",             new[] { "Item Icon Size", "物品图标大小" } },
            { "Fields.NameFontSize",         new[] { "Name Text Size", "名称字号" } },
            { "Fields.TypeFontSize",         new[] { "Type Text Size", "类型字号" } },
            { "Fields.DescriptionFontSize",  new[] { "Description Text Size", "描述字号" } },
            { "Fields.ModifierFontSize",     new[] { "Stat Modifier Text Size", "属性变化字号" } },
            { "Fields.StatsFontSize",        new[] { "Uses & Weight Text Size", "次数与重量字号" } },
            { "Advanced.HotReload",          new[] { "Live-Reload Config Files", "实时重载配置文件" } },
        };

        private static readonly Dictionary<string, string[]> SectionNames = new Dictionary<string, string[]>
        {
            { "Localization", new[] { "Language", "语言" } },
            { "Widget",       new[] { "Tooltip Window", "提示窗口" } },
            { "Fields",       new[] { "Tooltip Contents", "提示内容" } },
            { "Advanced",     new[] { "Advanced", "高级" } },
        };

        /**
         * @brief adds our tab button to a settings tab bar by cloning its General tab, once per bar
         */
        internal static void AddTabButton(SettingsTABS tabs)
        {
            if (tabs == null || tabs.transform.Find(TabName) != null) return;

            SettingsTABSButton template = null;
            foreach (SettingsTABSButton b in tabs.GetComponentsInChildren<SettingsTABSButton>(true))
            {
                if (b.name == TabName) return;
                if (template == null || b.category == SettingsCategory.General) template = b;
            }
            if (template == null) return;
            // kept as the look for the reset button, which has no settings cell of its own
            if (_tabTemplate == null) _tabTemplate = template.gameObject;

            GameObject go = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            go.name = TabName;
            go.transform.SetAsLastSibling();

            // the label is a localised game string; drop the localiser so our own text sticks
            foreach (LocalizedText lt in go.GetComponentsInChildren<LocalizedText>(true)) UnityEngine.Object.DestroyImmediate(lt);

            // drop any click wiring copied from the template (which would select its tab); TAB_Button.Start
            // adds the ButtonClicked listener that selects this one
            Button uiButton = go.GetComponent<Button>();
            if (uiButton != null) uiButton.onClick = new Button.ButtonClickedEvent();

            SettingsTABSButton button = go.GetComponent<SettingsTABSButton>();
            TabButtons.Add(button);
            button.category = Category;
            button.Selected = false;
            if (button.SelectedGraphic != null) button.SelectedGraphic.SetActive(false);
            if (button.text != null) button.text.text = TabLabel();
        }

        /**
         * @brief hides our scroll view and clears its rows on this page; called before every tab switch
         */
        internal static void Clear(SharedSettingsMenu menu)
        {
            if (!Pages.TryGetValue(menu, out Page page)) return;
            foreach (GameObject row in page.Rows)
                if (row != null) UnityEngine.Object.Destroy(row);
            page.Rows.Clear();
            if (page.Scroll != null) page.Scroll.SetActive(false);
        }

        /**
         * @brief fills our scroll view with a header per config section and a row per entry
         */
        internal static void Show(SharedSettingsMenu menu)
        {
            if (File == null || menu.m_settingsCellPrefab == null || menu.m_settingsContentParent == null)
            {
                Plugin.Log.LogWarning($"Item Tooltip settings tab cannot be filled (config={File != null}, " +
                                      $"rowPrefab={menu.m_settingsCellPrefab != null}, content={menu.m_settingsContentParent != null}).");
                return;
            }
            // without the input cells the rows still show, just without controls
            FindInputCells();

            Page page = EnsurePage(menu);
            FitScrollToPage(menu, page);
            page.Scroll.SetActive(true);

            // the tab label follows the language setting, so refresh it each time the tab is shown
            TabButtons.RemoveAll(b => b == null);
            foreach (SettingsTABSButton b in TabButtons)
                if (b.text != null) b.text.text = TabLabel();

            foreach (KeyValuePair<string, List<ConfigEntryBase>> section in CollectSections())
            {
                page.Rows.Add(MakeHeader(menu, page.Content, section.Key));
                foreach (ConfigEntryBase entry in section.Value)
                {
                    try { page.Rows.Add(MakeRow(menu, page.Content, entry)); }
                    catch (Exception e) { Plugin.Log.LogWarning($"Could not add a settings row for {entry.Definition}: {e}"); }
                }
            }

            try { AddResetRow(menu, page); }
            catch (Exception e) { Plugin.Log.LogWarning($"Could not add the reset-to-defaults row: {e}"); }

            page.ScrollRect.verticalNormalizedPosition = 1f;
        }

        /**
         * @brief adds a final row whose button resets every entry to its default. the first click arms it
         *        ("Click Again To Confirm"), the second resets and redraws the tab with the default values
         */
        private static void AddResetRow(SharedSettingsMenu menu, Page page)
        {
            // the game ships no button cell for its settings, so prefer one if a later version adds it and
            // otherwise build the button from a settings tab button, which has the same look as the tab bar
            GameObject template = _buttonCell != null ? _buttonCell : _tabTemplate;
            if (template == null)
            {
                Plugin.Log.LogWarning("Item Tooltip settings tab: no button to copy was found, so the reset row is hidden.");
                return;
            }

            GameObject row = MakeRow(menu, page.Content, Zh.IsActive() ? "恢复所有默认设置" : "Reset All Settings To Defaults",
                out Transform controlParent);
            page.Rows.Add(row);

            GameObject go = template == _buttonCell ? InstantiateStripped(_buttonCell, controlParent) : MakeButtonInDropdownSlot(controlParent);
            Button button = go.GetComponentInChildren<Button>(true);
            TextMeshProUGUI label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (button == null)
            {
                Plugin.Log.LogWarning("Item Tooltip settings tab: the game's button cell has no Button, so the reset row has no control.");
                return;
            }

            string idle = Zh.IsActive() ? "重置" : "RESET";
            string armed = Zh.IsActive() ? "再次点击确认" : "CLICK AGAIN TO CONFIRM";
            if (label != null) label.text = idle;

            bool confirm = false;
            button.onClick.AddListener(() =>
            {
                if (!confirm)
                {
                    confirm = true;
                    if (label != null) label.text = armed;
                    return;
                }

                foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> kv in File)
                    kv.Value.BoxedValue = kv.Value.DefaultValue;

                // redraw so every control shows its default
                Clear(menu);
                Show(menu);
            });
        }

        /**
         * @brief clones a settings tab button as a plain button: the tab scripts are removed (their click handler
         *        looks for a tab bar above it, and their update recolours the label for the selected state), the
         *        selected highlight is hidden, and the label is localiser-free
         */
        private static GameObject MakePlainButton(GameObject tabButton, Transform parent)
        {
            GameObject go = UnityEngine.Object.Instantiate(tabButton, parent);
            go.name = "ResetButton";
            go.SetActive(true);

            SettingsTABSButton tab = go.GetComponent<SettingsTABSButton>();
            if (tab != null)
            {
                if (tab.SelectedGraphic != null) tab.SelectedGraphic.SetActive(false);
                if (tab.text != null) tab.text.color = Color.white;
                UnityEngine.Object.DestroyImmediate(tab);
            }
            foreach (LocalizedText lt in go.GetComponentsInChildren<LocalizedText>(true)) UnityEngine.Object.DestroyImmediate(lt);

            Button button = go.GetComponent<Button>();
            if (button != null) button.onClick = new Button.ButtonClickedEvent();
            return go;
        }

        /**
         * @brief places a plain tab-style button exactly where the other rows put their dropdown: a dropdown cell
         *        is built as usual, the button takes the dropdown's place (same parent, anchors, size and layout
         *        sizing), and the dropdown is removed. the tab button's own rect comes from the tab bar, so
         *        without this it would sit wherever the tab bar layout had put it
         */
        private static GameObject MakeButtonInDropdownSlot(Transform controlParent)
        {
            if (_enumCell == null) return MakePlainButton(_tabTemplate, controlParent);

            GameObject cell = InstantiateStripped(_enumCell, controlParent);
            TMP_Dropdown dd = cell.GetComponentInChildren<TMP_Dropdown>(true);
            if (dd == null) return MakePlainButton(_tabTemplate, cell.transform);

            RectTransform slot = (RectTransform)dd.transform;
            GameObject go = MakePlainButton(_tabTemplate, slot.parent);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = slot.anchorMin;
            rt.anchorMax = slot.anchorMax;
            rt.pivot = slot.pivot;
            rt.anchoredPosition = slot.anchoredPosition;
            rt.sizeDelta = slot.sizeDelta;
            rt.localScale = slot.localScale;
            rt.SetSiblingIndex(slot.GetSiblingIndex());

            // when the slot is driven by a layout group, size the button the way the dropdown was sized
            LayoutElement slotLe = slot.GetComponent<LayoutElement>();
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = slotLe != null && slotLe.ignoreLayout;
            le.minWidth = slotLe != null ? slotLe.minWidth : -1f;
            le.minHeight = slotLe != null ? slotLe.minHeight : -1f;
            le.preferredWidth = slotLe != null && slotLe.preferredWidth > 0f ? slotLe.preferredWidth : slot.rect.width;
            le.preferredHeight = slotLe != null && slotLe.preferredHeight > 0f ? slotLe.preferredHeight : slot.rect.height;
            le.flexibleWidth = slotLe != null ? slotLe.flexibleWidth : -1f;
            le.flexibleHeight = slotLe != null ? slotLe.flexibleHeight : -1f;

            UnityEngine.Object.DestroyImmediate(dd.gameObject);
            return go;
        }

        private static string TabLabel()
        {
            return Zh.IsActive() ? "物品提示" : "ITEM TOOLTIP";
        }

        /**
         * @brief creates (once per page) a scroll view next to the page's content parent, with a content object
         *        laid out like the game's content parent
         */
        private static Page EnsurePage(SharedSettingsMenu menu)
        {
            if (Pages.TryGetValue(menu, out Page page) && page.Scroll != null) return page;

            page = new Page();
            Pages[menu] = page;

            Transform gameContent = menu.m_settingsContentParent;
            page.Scroll = new GameObject("PeakItemTooltipScroll");
            page.Scroll.transform.SetParent(gameContent.parent, false);
            page.Scroll.AddComponent<RectTransform>();
            page.Scroll.AddComponent<RectMask2D>();
            // invisible graphic so the wheel also scrolls over the gaps between rows
            page.Scroll.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            GameObject contentGo = new GameObject("Content");
            contentGo.transform.SetParent(page.Scroll.transform, false);
            page.Content = contentGo.AddComponent<RectTransform>();
            page.Content.anchorMin = new Vector2(0f, 1f);
            page.Content.anchorMax = new Vector2(1f, 1f);
            page.Content.pivot = new Vector2(0.5f, 1f);
            page.Content.sizeDelta = Vector2.zero;

            // match the game's row spacing when its content parent has a layout group
            VerticalLayoutGroup vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            VerticalLayoutGroup gameVlg = gameContent.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = gameVlg != null ? gameVlg.spacing : 10f;
            vlg.padding = gameVlg != null ? new RectOffset(gameVlg.padding.left, gameVlg.padding.right, gameVlg.padding.top, gameVlg.padding.bottom) : new RectOffset();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            page.ScrollRect = page.Scroll.AddComponent<ScrollRect>();
            page.ScrollRect.viewport = (RectTransform)page.Scroll.transform;
            page.ScrollRect.content = page.Content;
            page.ScrollRect.horizontal = false;
            page.ScrollRect.movementType = ScrollRect.MovementType.Clamped;
            page.ScrollRect.scrollSensitivity = 30f;

            return page;
        }

        /**
         * @brief places the scroll view over the game's content area: same left/right/top edges as the content
         *        parent, extended down to just above the bottom of the screen
         */
        private static void FitScrollToPage(SharedSettingsMenu menu, Page page)
        {
            RectTransform gameContent = (RectTransform)menu.m_settingsContentParent;
            RectTransform parent = (RectTransform)gameContent.parent;
            RectTransform scroll = (RectTransform)page.Scroll.transform;

            Canvas canvas = gameContent.GetComponentInParent<Canvas>();
            RectTransform canvasRt = canvas != null ? (RectTransform)canvas.rootCanvas.transform : parent;

            Vector3[] content = new Vector3[4];
            Vector3[] screen = new Vector3[4];
            gameContent.GetWorldCorners(content); // bottom-left, top-left, top-right, bottom-right
            canvasRt.GetWorldCorners(screen);

            Vector3 topLeft = parent.InverseTransformPoint(content[1]);
            Vector3 topRight = parent.InverseTransformPoint(content[2]);
            Vector3 screenTop = parent.InverseTransformPoint(screen[1]);
            Vector3 screenBottom = parent.InverseTransformPoint(screen[0]);
            // keep a margin above the screen's bottom edge, 5% of the screen height
            float bottom = screenBottom.y + (screenTop.y - screenBottom.y) * 0.05f;

            // anchoring at the parent's pivot makes anchoredPosition equal the parent-local position
            scroll.anchorMin = scroll.anchorMax = parent.pivot;
            scroll.pivot = new Vector2(0f, 1f);
            scroll.anchoredPosition = new Vector2(topLeft.x, topLeft.y);
            scroll.sizeDelta = new Vector2(topRight.x - topLeft.x, Mathf.Max(100f, topLeft.y - bottom));
            scroll.SetAsLastSibling();
        }

        /**
         * @brief groups the config file's entries by section, keeping bind order but putting Advanced last
         */
        private static List<KeyValuePair<string, List<ConfigEntryBase>>> CollectSections()
        {
            List<KeyValuePair<string, List<ConfigEntryBase>>> sections = new List<KeyValuePair<string, List<ConfigEntryBase>>>();
            Dictionary<string, List<ConfigEntryBase>> bySection = new Dictionary<string, List<ConfigEntryBase>>();

            foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> kv in File)
            {
                string section = kv.Key.Section;
                if (!bySection.TryGetValue(section, out List<ConfigEntryBase> list))
                {
                    list = new List<ConfigEntryBase>();
                    bySection[section] = list;
                    sections.Add(new KeyValuePair<string, List<ConfigEntryBase>>(section, list));
                }
                list.Add(kv.Value);
            }

            int advanced = sections.FindIndex(s => s.Key == "Advanced");
            if (advanced >= 0)
            {
                KeyValuePair<string, List<ConfigEntryBase>> s = sections[advanced];
                sections.RemoveAt(advanced);
                sections.Add(s);
            }
            return sections;
        }

        /**
         * @brief reads the dropdown and slider cell prefabs from the game's InputCellMapper asset
         */
        private static bool FindInputCells()
        {
            if (_enumCell != null && _floatCell != null) return true;

            // InputCellMapper derives from an odin type we do not reference, so resolve its type by name, load it
            // the way its SingletonAsset<T>.Instance does (Resources.Load by type name), and read its prefab
            // fields by reflection. an untyped load could return another asset that shares the name
            Type mapperType = Type.GetType("Zorro.Settings.InputCellMapper, Zorro.Settings.Runtime");
            if (mapperType == null)
            {
                Plugin.Log.LogWarning("Item Tooltip settings tab: could not resolve the InputCellMapper type.");
                return false;
            }

            UnityEngine.Object mapper = Resources.Load(mapperType.Name, mapperType);
            if (mapper == null)
            {
                UnityEngine.Object[] loaded = Resources.FindObjectsOfTypeAll(mapperType);
                if (loaded.Length > 0) mapper = loaded[0];
            }
            if (mapper == null)
            {
                Plugin.Log.LogWarning("Item Tooltip settings tab: could not find the game's InputCellMapper asset.");
                return false;
            }

            _enumCell = MapperField(mapper, "EnumSettingCell");
            _floatCell = MapperField(mapper, "FloatSettingCell");
            // optional: only the reset row needs it
            _buttonCell = MapperField(mapper, "ButtonSettingCell");
            if (_enumCell == null || _floatCell == null)
                Plugin.Log.LogWarning($"Item Tooltip settings tab: missing input cells (dropdown={_enumCell != null}, slider={_floatCell != null}).");
            return _enumCell != null && _floatCell != null;
        }

        private static GameObject MapperField(UnityEngine.Object mapper, string name)
        {
            FieldInfo f = mapper.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            return f != null ? f.GetValue(mapper) as GameObject : null;
        }

        /**
         * @brief adds a section header in the row label's font and colour, a little larger than the labels
         */
        private static GameObject MakeHeader(SharedSettingsMenu menu, Transform parent, string section)
        {
            TextMeshProUGUI template = menu.m_settingsCellPrefab.GetComponent<SettingsUICell>()?.m_text;

            GameObject go = new GameObject("Section_" + section);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            if (template != null)
            {
                t.font = template.font;
                t.fontSharedMaterial = template.fontSharedMaterial;
                t.color = template.color;
                t.fontSize = template.fontSize * 1.1f;
            }
            t.fontStyle = FontStyles.Normal;
            t.alignment = TextAlignmentOptions.BottomLeft;
            t.raycastTarget = false;
            t.text = Localised(SectionNames, section, section).ToUpperInvariant();
            go.AddComponent<LayoutElement>().preferredHeight = 56f;
            return go;
        }

        /**
         * @brief clones a settings row for the entry, labels it, and fills it with the matching control
         */
        private static GameObject MakeRow(SharedSettingsMenu menu, Transform parent, ConfigEntryBase entry)
        {
            string key = entry.Definition.Section + "." + entry.Definition.Key;
            GameObject row = MakeRow(menu, parent, Localised(Names, key, Humanise(entry.Definition.Key)), out Transform controlParent);
            row.name = "Row_" + entry.Definition.Key;
            AddControl(entry, controlParent);
            return row;
        }

        /**
         * @brief clones an empty, labelled settings row
         * @param label the row's label text
         * @param controlParent where the row's control goes
         */
        private static GameObject MakeRow(SharedSettingsMenu menu, Transform parent, string label, out Transform controlParent)
        {
            GameObject row = UnityEngine.Object.Instantiate(menu.m_settingsCellPrefab, parent);
            row.SetActive(true);

            SettingsUICell cell = row.GetComponent<SettingsUICell>();
            TextMeshProUGUI labelText = cell.m_text;
            controlParent = cell.m_settingsContentParent;
            if (cell.onlyOnMainMenu != null) cell.onlyOnMainMenu.SetActive(false);

            // the row's own scripts would fade it from zero alpha and overwrite the label with a localised
            // game string, so drop them and drive the row ourselves
            foreach (LocalizedText lt in row.GetComponentsInChildren<LocalizedText>(true)) UnityEngine.Object.DestroyImmediate(lt);
            UnityEngine.Object.DestroyImmediate(cell);
            CanvasGroup group = row.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;

            // keep the prefab's height inside our layout group
            RectTransform rt = (RectTransform)row.transform;
            LayoutElement le = row.GetComponent<LayoutElement>();
            if (le == null) le = row.AddComponent<LayoutElement>();
            if (le.preferredHeight <= 0f) le.preferredHeight = Mathf.Max(rt.sizeDelta.y, 60f);

            labelText.text = label;
            return row;
        }

        /**
         * @brief picks and wires the control for the entry's type
         */
        private static void AddControl(ConfigEntryBase entry, Transform parent)
        {
            if (_enumCell == null || _floatCell == null) return;
            Type type = entry.SettingType;

            if (type == typeof(bool))
            {
                string off = Zh.IsActive() ? "关闭" : "Off";
                string on = Zh.IsActive() ? "开启" : "On";
                AddDropdown(entry, parent, new object[] { false, true }, new[] { off, on });
            }
            else if (type == typeof(float) && entry.Description?.AcceptableValues is AcceptableValueRange<float> range)
            {
                AddSlider(entry, parent, range.MinValue, range.MaxValue);
            }
            else if (TryGetChoices(entry, out object[] choices))
            {
                string[] names = new string[choices.Length];
                for (int i = 0; i < choices.Length; i++) names[i] = choices[i].ToString();
                AddDropdown(entry, parent, choices, names);
            }
            else
            {
                AddTextField(entry, parent);
            }
        }

        private static void AddDropdown(ConfigEntryBase entry, Transform parent, object[] values, string[] names)
        {
            GameObject go = InstantiateStripped(_enumCell, parent);
            TMP_Dropdown dd = go.GetComponentInChildren<TMP_Dropdown>(true);

            dd.ClearOptions();
            dd.AddOptions(new List<string>(names));
            dd.SetValueWithoutNotify(Math.Max(0, Array.IndexOf(values, entry.BoxedValue)));
            dd.onValueChanged.AddListener(i => entry.BoxedValue = values[i]);
        }

        private static void AddSlider(ConfigEntryBase entry, Transform parent, float min, float max)
        {
            GameObject go = InstantiateStripped(_floatCell, parent);
            Slider slider = go.GetComponentInChildren<Slider>(true);
            TMP_InputField field = go.GetComponentInChildren<TMP_InputField>(true);

            // wide ranges (pixels, font sizes) step in whole numbers; narrow ones (scale, opacity) in hundredths
            bool whole = max - min > 20f;
            string format = whole ? "0" : "0.00";

            float value = (float)entry.BoxedValue;
            slider.wholeNumbers = whole;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            if (field != null) field.SetTextWithoutNotify(value.ToString(format, CultureInfo.InvariantCulture));

            slider.onValueChanged.AddListener(v =>
            {
                // round so dragging does not write long float tails into the cfg
                v = whole ? Mathf.Round(v) : Mathf.Round(v * 100f) / 100f;
                entry.BoxedValue = v;
                if (field != null) field.SetTextWithoutNotify(v.ToString(format, CultureInfo.InvariantCulture));
            });

            if (field == null) return;
            field.onEndEdit.AddListener(s =>
            {
                if (float.TryParse(s.Replace("%", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                {
                    v = Mathf.Clamp(v, min, max);
                    entry.BoxedValue = v;
                    slider.SetValueWithoutNotify(v);
                }
                field.SetTextWithoutNotify(((float)entry.BoxedValue).ToString(format, CultureInfo.InvariantCulture));
            });
        }

        /**
         * @brief a free-text value (e.g. the border colour hex): the float cell's input box with its slider hidden
         */
        private static void AddTextField(ConfigEntryBase entry, Transform parent)
        {
            GameObject go = InstantiateStripped(_floatCell, parent);
            Slider slider = go.GetComponentInChildren<Slider>(true);
            if (slider != null) slider.gameObject.SetActive(false);

            TMP_InputField field = go.GetComponentInChildren<TMP_InputField>(true);
            if (field == null) return;

            // the float cell's box only accepts numbers; open it up for text like "#FFFFFF"
            field.contentType = TMP_InputField.ContentType.Standard;
            field.characterValidation = TMP_InputField.CharacterValidation.None;
            field.characterLimit = 0;

            field.SetTextWithoutNotify(TomlTypeConverter.ConvertToString(entry.BoxedValue, entry.SettingType));
            // commit on end of edit so a half-typed value never reaches the config
            field.onEndEdit.AddListener(s =>
            {
                try { entry.BoxedValue = TomlTypeConverter.ConvertToValue(s, entry.SettingType); }
                catch { /* invalid; the old value stays and the text is reset below */ }
                field.SetTextWithoutNotify(TomlTypeConverter.ConvertToString(entry.BoxedValue, entry.SettingType));
            });
        }

        /**
         * @brief returns the fixed choices for an entry restricted to a value list, or for an enum
         */
        private static bool TryGetChoices(ConfigEntryBase entry, out object[] choices)
        {
            choices = null;
            if (entry.Description?.AcceptableValues is AcceptableValueBase acceptable)
            {
                // AcceptableValueList<T> exposes its values through AcceptableValues
                object values = Traverse.Create(acceptable).Property("AcceptableValues").GetValue();
                if (values is Array arr && arr.Length > 0)
                {
                    choices = new object[arr.Length];
                    arr.CopyTo(choices, 0);
                    return true;
                }
            }
            if (entry.SettingType.IsEnum)
            {
                Array arr = Enum.GetValues(entry.SettingType);
                choices = new object[arr.Length];
                arr.CopyTo(choices, 0);
                return true;
            }
            return false;
        }

        /**
         * @brief instantiates a game input cell and removes its Zorro SettingInputUICell script, leaving just the
         *        styled controls
         */
        private static GameObject InstantiateStripped(GameObject prefab, Transform parent)
        {
            GameObject go = UnityEngine.Object.Instantiate(prefab, parent);
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && IsSettingInputCell(mb.GetType())) UnityEngine.Object.DestroyImmediate(mb);
            }
            return go;
        }

        private static bool IsSettingInputCell(Type t)
        {
            for (; t != null; t = t.BaseType)
                if (t.Name == "SettingInputUICell") return true;
            return false;
        }

        /**
         * @brief picks the en or zh name from a name table, or the fallback when the key is not listed
         */
        private static string Localised(Dictionary<string, string[]> table, string key, string fallback)
        {
            if (!table.TryGetValue(key, out string[] names)) return fallback;
            return Zh.IsActive() ? names[1] : names[0];
        }

        /**
         * @brief splits a PascalCase key into words, e.g. "ShowWhileHolding" -> "Show While Holding"
         */
        private static string Humanise(string key)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < key.Length; i++)
            {
                char ch = key[i];
                bool boundary = i > 0 && char.IsUpper(ch) && (char.IsLower(key[i - 1]) || (i + 1 < key.Length && char.IsLower(key[i + 1])));
                if (boundary) sb.Append(' ');
                sb.Append(ch);
            }
            return sb.ToString();
        }
    }

    /**
     * @brief adds the tab button before the tab bar's Start collects its buttons
     */
    [HarmonyPatch(typeof(SettingsTABS), "OnEnable")]
    internal static class SettingsTabsOnEnablePatch
    {
        private static void Postfix(SettingsTABS __instance)
        {
            try { SettingsTab.AddTabButton(__instance); }
            catch (Exception e) { Plugin.Log.LogWarning($"Could not add the Item Tooltip settings tab: {e}"); }
        }
    }

    /**
     * @brief on every tab switch clears our rows; for our tab, also clears the game's rows and shows ours instead
     */
    [HarmonyPatch(typeof(SharedSettingsMenu), nameof(SharedSettingsMenu.ShowSettings))]
    internal static class SharedSettingsMenuShowSettingsPatch
    {
        private static readonly AccessTools.FieldRef<SharedSettingsMenu, List<SettingsUICell>> SpawnedCells =
            AccessTools.FieldRefAccess<SharedSettingsMenu, List<SettingsUICell>>("m_spawnedCells");
        private static readonly AccessTools.FieldRef<SharedSettingsMenu, Coroutine> FadeIn =
            AccessTools.FieldRefAccess<SharedSettingsMenu, Coroutine>("m_fadeInCoroutine");

        private static bool Prefix(SharedSettingsMenu __instance, SettingsCategory category)
        {
            SettingsTab.Clear(__instance);
            if (category != SettingsTab.Category) return true;

            // mirror the start of the game's ShowSettings: stop the fade-in and remove the previous tab's cells
            if (FadeIn(__instance) != null)
            {
                __instance.StopCoroutine(FadeIn(__instance));
                FadeIn(__instance) = null;
            }
            foreach (SettingsUICell cell in SpawnedCells(__instance))
                if (cell != null) UnityEngine.Object.Destroy(cell.gameObject);
            SpawnedCells(__instance).Clear();

            try { SettingsTab.Show(__instance); }
            catch (Exception e) { Plugin.Log.LogWarning($"Could not show the Item Tooltip settings tab: {e}"); }
            return false;
        }
    }
}
