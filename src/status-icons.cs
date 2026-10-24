/**
 * @authors @katcw
 * @file    status-icons.cs
 * @brief   looks up the hud's status icons so modifier lines can show them in place of status names
 */
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PeakItemTooltip
{
    /**
     * @brief borrows the status icons from the hud stamina bar (GUIManager.instance.bar), one per status type
     *        plus the extra-stamina icon
     */
    public static class StatusIcons
    {
        // the hud's own icon images; sprite and tint are read from them so the tooltip matches the bar exactly
        private static readonly Dictionary<CharacterAfflictions.STATUSTYPE, Image> Icons = new Dictionary<CharacterAfflictions.STATUSTYPE, Image>();
        private static Image _stamina;

        // the bar the cache was filled from; the hud is rebuilt per scene, so refill when it changes
        private static StaminaBar _source;

        /**
         * @brief looks up the hud icon for a modifier line
         * @param line the line whose status icon is wanted
         * @param sprite the icon sprite
         * @param tint the colour the hud draws the icon with
         * @return false when the hud (or that icon) is not available
         */
        public static bool TryGet(ModifierLine line, out Sprite sprite, out Color tint)
        {
            Refresh();
            Image img = null;
            if (line.IsStamina) img = _stamina;
            else Icons.TryGetValue(line.Status, out img);

            sprite = img != null ? img.sprite : null;
            tint = img != null ? img.color : Color.white;
            return sprite != null;
        }

        /**
         * @brief refills the cache from the current hud stamina bar when it has changed
         */
        private static void Refresh()
        {
            StaminaBar bar = GUIManager.instance != null ? GUIManager.instance.bar : null;
            if (bar == _source) return;

            _source = bar;
            Icons.Clear();
            _stamina = null;
            if (bar == null) return;

            if (bar.afflictions != null)
                foreach (BarAffliction a in bar.afflictions) Add(a);

            // petrify lives on its own bar segment rather than in the afflictions array; also sweep every
            // segment under the bar in case one (e.g. weight) is not listed in the array
            Add(bar.petrifyAffliction);
            foreach (BarAffliction a in bar.GetComponentsInChildren<BarAffliction>(true)) Add(a);

            _stamina = bar.extraStaminaIcon;

            List<string> found = new List<string>();
            foreach (KeyValuePair<CharacterAfflictions.STATUSTYPE, Image> kv in Icons) found.Add(kv.Key + "=" + kv.Value.sprite.name);
            Plugin.Log.LogInfo("Status icons found: " + string.Join(", ", found));
        }

        /**
         * @brief caches a bar segment's icon unless it has no sprite or its status already has one
         */
        private static void Add(BarAffliction a)
        {
            if (a == null || a.icon == null || a.icon.sprite == null) return;
            // the petrify segment may not have its afflictionType set, so key it by its flag
            CharacterAfflictions.STATUSTYPE type = a.isPetrify ? CharacterAfflictions.STATUSTYPE.Petrify : a.afflictionType;
            if (!Icons.ContainsKey(type)) Icons[type] = a.icon;
        }
    }
}
