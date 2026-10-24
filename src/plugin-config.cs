/**
 * @authors @katcw, @gintoki000 (for chinese simplified translations)
 * @file    plugin-config.cs
 * @brief   binds the widget's configurable options to the bepinex config file
 */
using BepInEx.Configuration;

namespace PeakItemTooltip
{
    /**
     * @brief holds every configurable widget and field option, bound to the bepinex config file
     */
    public class PluginConfig
    {
        // tooltip label language: Auto (follow game) / zh-CN / en
        public static ConfigEntry<string> LanguageMode;

        public readonly ConfigEntry<bool> Enabled;

        // DEV/TUNING: when on, the plugin polls the config + descriptions files on disk so
        // edits apply live without restarting
        public readonly ConfigEntry<bool> HotReload;

        // if ON, tooltip appears for held AND hovered items
        // if OFF, tooltip only appears for hovered items
        public readonly ConfigEntry<bool> ShowWhileHolding;

        // placement/sizing: offsets and width are in 1920x1080 reference pixels
        // CanvasScaler converts them to user resolution
        public readonly ConfigEntry<float> OffsetX;
        public readonly ConfigEntry<float> OffsetY;
        public readonly ConfigEntry<float> Scale;
        public readonly ConfigEntry<float> Width;
        public readonly ConfigEntry<float> BackgroundOpacity;
        // added border colour and opacity config settings
        public readonly ConfigEntry<string> BorderColour;
        public readonly ConfigEntry<float>  BorderOpacity;

        public readonly ConfigEntry<bool>  ShowIcon;
        public readonly ConfigEntry<bool>  ShowName;
        public readonly ConfigEntry<bool>  ShowType;
        public readonly ConfigEntry<bool>  ShowDescription;
        public readonly ConfigEntry<bool>  ShowModifiers;
        public readonly ConfigEntry<bool>  ShowStatusIcons;
        public readonly ConfigEntry<bool>  ShowUses;
        // remaining uses are hidden on the held item by default (issue #4); this opts back in
        public readonly ConfigEntry<bool>  ShowUsesWhileHolding;
        public readonly ConfigEntry<bool>  ShowWeight;
        public readonly ConfigEntry<float> IconSize;
        public readonly ConfigEntry<float> NameFontSize;
        public readonly ConfigEntry<float> TypeFontSize;
        public readonly ConfigEntry<float> DescriptionFontSize;
        public readonly ConfigEntry<float> ModifierFontSize;
        public readonly ConfigEntry<float> StatsFontSize;

        /**
         * @brief binds every widget and field option to the given config file with its default and description
         * @param cfg the bepinex config file to bind the entries to
         */
        public PluginConfig(ConfigFile cfg)
        {
            LanguageMode = cfg.Bind("Localization", "Language", "Auto",
                new ConfigDescription("Tooltip label language: Auto (follow game language) / zh-CN (force Simplified Chinese) / en (force English).",
                new AcceptableValueList<string>("Auto", "zh-CN", "en")));

            Enabled = cfg.Bind("Widget", "Enabled", true,
                "Master toggle for the hover info widget.");

            HotReload = cfg.Bind("Advanced", "HotReload", true,
                "Watch the config and descriptions files for changes and apply them live without " +
                "restarting the game. Handy while tuning; safe to disable if you never edit these files mid-session. " +
                "Note: turning this back ON requires a game restart to take effect (while off, nothing is watching the file).");
            ShowWhileHolding = cfg.Bind("Widget", "ShowWhileHolding", true,
                "Also show the tooltip for the item you are holding. If off, it only appears when hovering.");

            // ranges let the settings tab show sliders; the offsets span the 1920x1080 reference screen
            OffsetX = cfg.Bind("Widget", "OffsetX", -710f,
                Range("Horizontal offset from the screen center; positive = right (1920x1080 reference pixels).", -960f, 960f));
            OffsetY = cfg.Bind("Widget", "OffsetY", -220f,
                Range("Vertical offset from the screen center; positive = up (1920x1080 reference pixels).", -540f, 540f));
            Scale = cfg.Bind("Widget", "Scale", 1.15f,
                new ConfigDescription("Overall scale multiplier.",
                new AcceptableValueRange<float>(0.1f, 5f)));
            Width = cfg.Bind("Widget", "Width", 320f,
                Range("Widget width in reference pixels; the description wraps to this width.", 150f, 800f));
            BackgroundOpacity = cfg.Bind("Widget", "BackgroundOpacity", 0.75f,
                new ConfigDescription("Background panel opacity.",
                new AcceptableValueRange<float>(0f, 1f)));
            BorderColour = cfg.Bind("Widget", "BorderColour", "#FFFFFF",
                "Colour of the outline around the widget, as a hex string (e.g. #FFFFFF, #808080). Alpha is controlled by BorderOpacity.");
            BorderOpacity = cfg.Bind("Widget", "BorderOpacity", 1.0f,
                new ConfigDescription("Opacity of the outline around the widget :)",
                new AcceptableValueRange<float>(0f, 1f)));

            ShowIcon = cfg.Bind("Fields", "ShowIcon", true, "Show the item icon.");
            ShowName = cfg.Bind("Fields", "ShowName", true, "Show the item name.");
            ShowType = cfg.Bind("Fields", "ShowType", true, "Show the item type (e.g. Mystical, Consumable).");
            ShowDescription = cfg.Bind("Fields", "ShowDescription", true, "Show the description.");
            ShowModifiers = cfg.Bind("Fields", "ShowModifiers", true, "Show item stat modifiers (e.g. +10 Poison).");
            ShowStatusIcons = cfg.Bind("Fields", "ShowStatusIcons", false,
                "Show the status bar icon in place of the status name on modifier lines (e.g. [icon] +10 instead of +10 Poison).");
            ShowUses = cfg.Bind("Fields", "ShowUses", true,
                "Show remaining uses or fuel (e.g. Uses 2/3, Rope 40/60, Fuel 80% 96s).");
            ShowUsesWhileHolding = cfg.Bind("Fields", "ShowUsesWhileHolding", false,
                "Also show remaining uses for the item you are holding. If off, uses only show for hovered/dropped items.");
            ShowWeight = cfg.Bind("Fields", "ShowWeight", true,
                "Show how much the item adds to the weight bar when carried.");
            IconSize = cfg.Bind("Fields", "IconSize", 48f, Range("Icon width/height in reference pixels.", 16f, 128f));
            NameFontSize = cfg.Bind("Fields", "NameFontSize", 22f, Range("Name font size.", 8f, 48f));
            TypeFontSize = cfg.Bind("Fields", "TypeFontSize", 15f, Range("Type font size.", 8f, 48f));
            DescriptionFontSize = cfg.Bind("Fields", "DescriptionFontSize", 16f, Range("Description font size.", 8f, 48f));
            ModifierFontSize = cfg.Bind("Fields", "ModifierFontSize", 16f, Range("Modifier font size.", 8f, 48f));
            StatsFontSize = cfg.Bind("Fields", "StatsFontSize", 15f, Range("Uses and weight font size.", 8f, 48f));
        }

        /**
         * @brief a description limited to a float range
         */
        private static ConfigDescription Range(string description, float min, float max)
        {
            return new ConfigDescription(description, new AcceptableValueRange<float>(min, max));
        }
    }
}
