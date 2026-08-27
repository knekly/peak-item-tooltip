/**
 * @authors @katcw, @gintoki000 (for chinese simplified translations)
 * @file    item-modifiers.cs
 * @brief   reads an item's effect components and formats them as signed, colour-coded stat modifier lines
 */
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace PeakItemTooltip
{
    /**
     * @brief reads a consumable item's effect components and formats them as signed, colour-coded lines, scaling fractions to points
     */
    public static class ItemModifiers
    {
        // status display colours
        private static readonly Dictionary<CharacterAfflictions.STATUSTYPE, Color> Colours = new Dictionary<CharacterAfflictions.STATUSTYPE, Color>
        {
            { CharacterAfflictions.STATUSTYPE.Injury,  Hex("#E4463B") },
            { CharacterAfflictions.STATUSTYPE.Hunger,  Hex("#F2C43D") },
            { CharacterAfflictions.STATUSTYPE.Cold,    Hex("#4FA3FF") },
            { CharacterAfflictions.STATUSTYPE.Poison,  Hex("#A65AD8") },
            { CharacterAfflictions.STATUSTYPE.Crab,    Hex("#FF7043") },
            { CharacterAfflictions.STATUSTYPE.Curse,   Hex("#B5179E") },
            { CharacterAfflictions.STATUSTYPE.Drowsy,  Hex("#5AD1C4") },
            { CharacterAfflictions.STATUSTYPE.Weight,  Hex("#B0A08A") },
            { CharacterAfflictions.STATUSTYPE.Hot,     Hex("#FF8C1A") },
            { CharacterAfflictions.STATUSTYPE.Thorns,  Hex("#6FBF4B") },
            { CharacterAfflictions.STATUSTYPE.Spores,  Hex("#9CCC65") },
            { CharacterAfflictions.STATUSTYPE.Web,     Hex("#D0D0D0") },
            { CharacterAfflictions.STATUSTYPE.Arrow,   Hex("#C9A66B") },
            { CharacterAfflictions.STATUSTYPE.Petrify, Hex("#9E9E9E") },
            { CharacterAfflictions.STATUSTYPE.FlyTrap, Hex("#4CAF50") },
        };

        // since extra stamina is not a STATUSTYPE, assign its own colour
        private static readonly Color StaminaColour = Hex("#7CD64B");
        private static readonly Color Fallback = Color.white;

        /**
         * @brief builds a rich-text block of the item's stat modifiers, one coloured line per modifier, or an empty string if it has none
         */
        public static string Format(Item item)
        {
            if (item == null) return "";

            StringBuilder sb = new StringBuilder();
            bool first = true;

            // order from top to bottom is as such: [1] hunger, [2] poison, [3] weight/other, [4] stamina, [5] petrify

            // SUBTRACTED hunger: negative net change in hunger
            foreach (Action_RestoreHunger a in item.GetComponentsInChildren<Action_RestoreHunger>(true))
            {
                if (a == null) continue;
                AppendLine(sb, ref first, ColourFor(CharacterAfflictions.STATUSTYPE.Hunger), -a.restorationAmount, Zh.IsActive() ? "饥饿" : "Hunger");
            }

            // ADDED poision over time: total applied = poison per second * time
            foreach (Action_InflictPoison a in item.GetComponentsInChildren<Action_InflictPoison>(true))
            {
                if (a == null) continue;
                AppendLine(sb, ref first, ColourFor(CharacterAfflictions.STATUSTYPE.Poison), a.poisonPerSecond * a.inflictionTime, Zh.IsActive() ? "中毒" : "Poison");
            }

            // signed generic status changes: covers weight and anything else)t.
            foreach (Action_ModifyStatus a in item.GetComponentsInChildren<Action_ModifyStatus>(true))
            {
                if (a == null) continue;
                AppendLine(sb, ref first, ColourFor(a.statusType), a.changeAmount, StatusName(a.statusType));
            }

            // ADDED extra stamina
            foreach (Action_GiveExtraStamina a in item.GetComponentsInChildren<Action_GiveExtraStamina>(true))
            {
                if (a == null) continue;
                AppendLine(sb, ref first, StaminaColour, a.amount, Zh.IsActive() ? "额外耐力" : "Extra Stamina");
            }

            // ADDED petrify: unlike other statuses, petrify is never applied through Action_ModifyStatus.
            // amulets call AddPetrify(int)/AddStatus(Petrify, float) directly, storing the cost in fields
            // named petrify* on their components (e.g. Action_CloneSelectedItem.petrify, DoubleJumpAmulet.
            // petrifyPerJump). read those fields off the item's components so every petrify source shows up
            AppendPetrify(sb, ref first, item);

            return sb.ToString();
        }

        /**
         * @brief scans the item's components for petrify cost fields and appends one line per field found
         * @param sb the builder the lines are appended to
         * @param first whether the next line is the first; updated as lines are appended
         * @param item the item whose components are scanned
         */
        private static void AppendPetrify(StringBuilder sb, ref bool first, Item item)
        {
            Color colour = ColourFor(CharacterAfflictions.STATUSTYPE.Petrify);

            foreach (MonoBehaviour comp in item.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null) continue;

                foreach (FieldInfo f in comp.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!f.Name.StartsWith("petrify", StringComparison.OrdinalIgnoreCase)) continue;

                    // int petrify fields are already in 0-100 points; float fields are on the 0-1 status
                    // scale (AddStatus floors amount*100 into points), so bring both to points
                    int points;
                    if (f.FieldType == typeof(int)) points = (int)f.GetValue(comp);
                    else if (f.FieldType == typeof(float)) points = Mathf.RoundToInt((float)f.GetValue(comp) * 100f);
                    else continue;

                    AppendPoints(sb, ref first, colour, points, PetrifyLabel(f.Name));
                }
            }

            // the healing amulet (scout's tenacity) petrifies by clamp(heal * ratio, minPetrify, maxPetrify),
            // so the actual cost varies with how much is healed; a range rather than a flat cost, so handle it
            // on its own and show the min-max bounds
            foreach (Peak.Action_HealingGem a in item.GetComponentsInChildren<Peak.Action_HealingGem>(true))
            {
                if (a == null) continue;
                int low = Mathf.RoundToInt(a.minPetrify * 100f);
                int high = Mathf.RoundToInt(a.maxPetrify * 100f);
                AppendPointsRange(sb, ref first, colour, low, high, Zh.IsActive() ? "石化" : "Petrify");
            }
        }

        /**
         * @brief turns a petrify field name into a display label, e.g. "petrifyPerJump" -> "Petrify (per jump)"
         * @param fieldName the reflected field name, always starting with "petrify"
         * @return the humanised label
         */
        private static string PetrifyLabel(string fieldName)
        {
            string rest = fieldName.Length > "petrify".Length ? fieldName.Substring("petrify".Length) : "";
            if (string.IsNullOrEmpty(rest)) return Zh.IsActive() ? "石化" : "Petrify";

            // split the camel-cased remainder into lowercase words: "PerJump" -> "per jump"
            StringBuilder qualifier = new StringBuilder();
            foreach (char ch in rest)
            {
                if (char.IsUpper(ch) && qualifier.Length > 0) qualifier.Append(' ');
                qualifier.Append(char.ToLowerInvariant(ch));
            }

            return Zh.IsActive() ? "石化（" + QualifierZh(qualifier.ToString()) + "）" : "Petrify (" + qualifier + ")";
        }

        /**
         * @brief localised display name for a status type (zh-CN patch)
         */
        private static string StatusName(CharacterAfflictions.STATUSTYPE type)
        {
            if (!Zh.IsActive()) return type.ToString();
            switch (type)
            {
                case CharacterAfflictions.STATUSTYPE.Injury:  return "受伤";
                case CharacterAfflictions.STATUSTYPE.Hunger:  return "饥饿";
                case CharacterAfflictions.STATUSTYPE.Cold:    return "寒冷";
                case CharacterAfflictions.STATUSTYPE.Poison:  return "中毒";
                case CharacterAfflictions.STATUSTYPE.Crab:    return "螃蟹";
                case CharacterAfflictions.STATUSTYPE.Curse:   return "诅咒";
                case CharacterAfflictions.STATUSTYPE.Drowsy:  return "困倦";
                case CharacterAfflictions.STATUSTYPE.Weight:  return "负重";
                case CharacterAfflictions.STATUSTYPE.Hot:     return "炎热";
                case CharacterAfflictions.STATUSTYPE.Thorns:  return "荆棘";
                case CharacterAfflictions.STATUSTYPE.Spores:  return "孢子";
                case CharacterAfflictions.STATUSTYPE.Web:     return "蛛网";
                case CharacterAfflictions.STATUSTYPE.Arrow:   return "中箭";
                case CharacterAfflictions.STATUSTYPE.Petrify: return "石化";
                case CharacterAfflictions.STATUSTYPE.FlyTrap: return "捕蝇草";
                default: return type.ToString();
            }
        }

        /**
         * @brief localised suffix for petrify field names, e.g. "per jump" -> "每次跳跃" (zh-CN patch)
         */
        private static string QualifierZh(string qualifier)
        {
            if (!Zh.IsActive()) return qualifier;
            switch (qualifier)
            {
                case "per jump":   return "每次跳跃";
                case "per use":    return "每次使用";
                case "per throw":  return "每次投掷";
                case "per second": return "每秒";
                case "per bounce": return "每次弹跳";
                case "on hit":     return "命中时";
                case "on land":    return "落地时";
                case "on heal":    return "治疗时";
                case "on burn":    return "燃烧时";
                default: return qualifier;
            }
        }

        /**
         * @brief appends one signed, coloured modifier line, scaling the 0-1 fraction to display points and skipping zero-point changes
         * @param sb the builder the line is appended to
         * @param first whether this is the first line; set to false after the first append so later lines are newline-separated
         * @param colour the colour applied to the line via a rich-text tag
         * @param signedFraction the net change on the 0-1 scale; its sign sets the leading + or -
         * @param label the status name shown after the value
         */
        private static void AppendLine(StringBuilder sb, ref bool first, Color colour,
            float signedFraction, string label)
        {
            // scale the 0-1 fraction to display points, keeping the sign
            AppendPoints(sb, ref first, colour, Mathf.RoundToInt(signedFraction * 100f), label);
        }

        /**
         * @brief appends one signed, coloured modifier line from an already-scaled points value, skipping zero
         * @param sb the builder the line is appended to
         * @param first whether this is the first line; set to false after the first append so later lines are newline-separated
         * @param colour the colour applied to the line via a rich-text tag
         * @param signedPoints the net change in display points; its sign sets the leading + or -
         * @param label the status name shown after the value
         */
        private static void AppendPoints(StringBuilder sb, ref bool first, Color colour,
            int signedPoints, string label)
        {
            if (signedPoints == 0) return;

            string sign = signedPoints > 0 ? "+" : "-";
            string hex = ColorUtility.ToHtmlStringRGB(colour);

            if (!first) sb.Append('\n');
            first = false;

            sb.Append("<color=#").Append(hex).Append('>')
              .Append(sign).Append(Mathf.Abs(signedPoints)).Append(' ').Append(label)
              .Append("</color>");
        }

        /**
         * @brief appends a coloured modifier line for a value that varies within a range, as "+low–high label",
         *        collapsing to a single value when the bounds are equal and skipping an all-zero range
         * @param sb the builder the line is appended to
         * @param first whether this is the first line; set to false after the first append
         * @param colour the colour applied to the line via a rich-text tag
         * @param lowPoints the lower bound in display points (assumed non-negative)
         * @param highPoints the upper bound in display points (assumed non-negative)
         * @param label the status name shown after the value
         */
        private static void AppendPointsRange(StringBuilder sb, ref bool first, Color colour,
            int lowPoints, int highPoints, string label)
        {
            if (lowPoints == highPoints)
            {
                AppendPoints(sb, ref first, colour, highPoints, label);
                return;
            }
            if (highPoints == 0) return;

            string hex = ColorUtility.ToHtmlStringRGB(colour);

            if (!first) sb.Append('\n');
            first = false;

            sb.Append("<color=#").Append(hex).Append('>')
              .Append('+').Append(lowPoints).Append('–').Append(highPoints).Append(' ').Append(label)
              .Append("</color>");
        }

        /**
         * @brief returns the display colour for a status type, falling back to white if it is not mapped
         * @param type the status type to colour
         * @return the mapped colour, or white when unmapped
         */
        private static Color ColourFor(CharacterAfflictions.STATUSTYPE type)
        {
            return Colours.TryGetValue(type, out Color c) ? c : Fallback;
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
