/**
 * @authors @katcw, @gintoki000 (for chinese simplified translations)
 * @file    item-stats.cs
 * @brief   reads an item's remaining uses / fuel and carry weight and formats them as tooltip lines
 */
using System.Globalization;
using UnityEngine;

namespace PeakItemTooltip
{
    /**
     * @brief formats an item's remaining uses (count, rope length or fuel) and its carry weight
     */
    public static class ItemStats
    {
        // matches the Weight status colour in ItemModifiers so the weight line reads as the same stat
        private static readonly Color WeightColour = Hex("#B0A08A");
        private static readonly Color UsesColour = Hex("#E0E0E0");

        /**
         * @brief builds the remaining-uses line, or an empty string when the item has no limited uses
         * @param item the item to read
         * @return e.g. "Uses: 2/3", "Rope: 40/60", "Fuel: 80% (96s)"
         */
        public static string FormatUses(Item item)
        {
            if (item == null) return "";

            // all reads go through item.data directly rather than Item.GetData, which would register
            // missing entries on the item as a side effect

            // lantern: fuel burns one unit per second while lit, so remaining fuel is also seconds of light
            Lantern lantern = item.GetComponent<Lantern>();
            if (lantern != null && lantern.startingFuel > 0f)
            {
                float fuel = TryGetFloat(item, DataEntryKey.Fuel, out float f) ? f : lantern.startingFuel;
                int percent = Mathf.RoundToInt(Mathf.Clamp01(fuel / lantern.startingFuel) * 100f);
                int seconds = Mathf.CeilToInt(Mathf.Max(0f, fuel));
                return Line(UsesColour, (Zh.IsActive() ? "燃料：" : "Fuel: ") + percent + "% (" + seconds + "s)");
            }

            // rope spool: fuel is the remaining rope length in segments
            RopeSpool spool = item.GetComponent<RopeSpool>();
            if (spool != null && spool.ropeStartFuel > 0f)
            {
                float fuel = TryGetFloat(item, DataEntryKey.Fuel, out float f) ? f : spool.ropeStartFuel;
                int left = Mathf.FloorToInt(Mathf.Max(0f, fuel));
                int total = Mathf.RoundToInt(spool.ropeStartFuel);
                return Line(UsesColour, (Zh.IsActive() ? "绳长：" : "Rope: ") + left + "/" + total);
            }

            // count-based uses (e.g. cure-all): totalUses is the max, the ItemUses entry the remaining count
            if (item.totalUses > 0 && item.data != null
                && item.data.TryGetDataEntry(DataEntryKey.ItemUses, out OptionableIntItemData uses)
                && uses.HasData && uses.Value >= 0)
            {
                return Line(UsesColour, (Zh.IsActive() ? "剩余次数：" : "Uses: ") + uses.Value + "/" + item.totalUses);
            }

            // anything else that tracks a remaining fraction (other fuel-style items)
            if (TryGetFloat(item, DataEntryKey.UseRemainingPercentage, out float pct))
            {
                int percent = Mathf.RoundToInt(Mathf.Clamp01(pct) * 100f);
                return Line(UsesColour, (Zh.IsActive() ? "剩余：" : "Remaining: ") + percent + "%");
            }

            return "";
        }

        /**
         * @brief returns the item's signed weight in weight-bar points, or an empty string for weightless items
         * @param item the item to read
         * @return e.g. "+2.5"
         */
        public static string WeightValue(Item item)
        {
            if (item == null) return "";

            // the weight bar is set to STATUS_INCREMENT per carry-weight unit; scale to display points
            // like the modifier lines do (0-1 status -> 0-100 points)
            float points = item.CarryWeight * CharacterAfflictions.STATUS_INCREMENT * 100f;
            if (Mathf.Approximately(points, 0f)) return "";

            // signed like the modifier lines: carrying the item adds (or removes) this much weight
            return (points > 0f ? "+" : "-") + Mathf.Abs(points).ToString("0.#", CultureInfo.InvariantCulture);
        }

        /**
         * @brief builds the weight line from a WeightValue result
         * @param value the weight in points
         * @param iconForLabel true when the weight icon is shown in place of the "Weight:" label
         * @return e.g. "Weight: +2.5", or just "+2.5" in icon mode
         */
        public static string FormatWeight(string value, bool iconForLabel)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (iconForLabel) return Line(WeightColour, value);
            return Line(WeightColour, (Zh.IsActive() ? "重量：" : "Weight: ") + value);
        }

        /**
         * @brief reads a float data entry from the item's instance data without creating it
         */
        private static bool TryGetFloat(Item item, DataEntryKey key, out float value)
        {
            value = 0f;
            if (item.data == null || !item.data.TryGetDataEntry(key, out FloatItemData d)) return false;
            value = d.Value;
            return true;
        }

        /**
         * @brief wraps text in a rich-text colour tag
         */
        private static string Line(Color colour, string text)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(colour) + ">" + text + "</color>";
        }

        /**
         * @brief parses a hex colour string into a colour.
         */
        private static Color Hex(string s)
        {
            ColorUtility.TryParseHtmlString(s, out Color c);
            return c;
        }
    }
}
