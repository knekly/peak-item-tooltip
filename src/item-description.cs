using System;
using System.Collections.Generic;
using System.Reflection;

namespace PeakItemTooltip
{
    /// <summary>
    /// Values used to render one tooltip refresh. Capturing the state and modifier text together
    /// prevents the description and numeric rows from observing different cooking updates.
    /// </summary>
    public sealed class TooltipItemSnapshot
    {
        public readonly string CookingState;
        public readonly string Modifiers;
        public readonly HashSet<string> ActivePositiveStatuses;
        public readonly bool SafetySourcesComplete;

        private TooltipItemSnapshot(string cookingState, string modifiers, HashSet<string> activePositiveStatuses,
            bool safetySourcesComplete)
        {
            CookingState = cookingState;
            Modifiers = modifiers;
            ActivePositiveStatuses = activePositiveStatuses;
            SafetySourcesComplete = safetySourcesComplete;
        }

        public static TooltipItemSnapshot Capture(Item item)
        {
            if (item == null)
                return new TooltipItemSnapshot("raw", "", new HashSet<string>(StringComparer.OrdinalIgnoreCase), true);
            return new TooltipItemSnapshot(ItemDescription.StateFor(item), ItemModifiers.Format(item),
                ItemModifiers.ActivePositiveStatuses(item), ItemModifiers.HasCompleteSafetyCoverage(item));
        }

        public bool HasActivePositiveStatus(string status) =>
            !string.IsNullOrEmpty(status) && ActivePositiveStatuses.Contains(status);
    }

    public static class ItemDescription
    {
        private static readonly FieldInfo OverrideUsabilityField = typeof(Item).GetField(
            "overrideUsability", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MemberInfo OverrideUsabilityIsNone = FindMember(OverrideUsabilityField?.FieldType, "IsNone");
        private static readonly MemberInfo OverrideUsabilityValue = FindMember(OverrideUsabilityField?.FieldType, "Value");

        public static string Format(Item item, string baseDescription, string context,
            IDictionary<string, string> stateDescriptions)
        {
            if (item == null) return baseDescription;

            bool hasCuratedProfile = !string.IsNullOrWhiteSpace(context)
                || (stateDescriptions != null && stateDescriptions.Count > 0);
            return FormatForState(StateFor(item), baseDescription, context, stateDescriptions, hasCuratedProfile);
        }

        public static string FormatSnapshot(TooltipItemSnapshot snapshot, string baseDescription, string context,
            IDictionary<string, string> stateDescriptions, IDictionary<string, StateCheck> stateChecks)
        {
            if (snapshot == null) return baseDescription;

            string state = snapshot.CookingState;
            string stateDescription = null;
            if (stateDescriptions != null) stateDescriptions.TryGetValue(state, out stateDescription);
            bool hasSafetyClaim = !string.IsNullOrWhiteSpace(stateDescription);
            bool hasKnownConflict = false;
            StateCheck check = null;
            bool hasStateCheck = stateChecks != null && stateChecks.TryGetValue(state, out check);

            if (hasStateCheck && check != null && check.MustNotHaveActive != null)
            {
                foreach (string status in check.MustNotHaveActive)
                {
                    if (snapshot.HasActivePositiveStatus(status))
                    {
                        stateDescription = null;
                        hasKnownConflict = true;
                        break;
                    }
                }

                if (!snapshot.SafetySourcesComplete)
                    stateDescription = null;
            }

            bool hasCuratedProfile = !string.IsNullOrWhiteSpace(context)
                || (stateDescriptions != null && stateDescriptions.Count > 0);
            string formatted = FormatForState(state, baseDescription, context, stateDescription, hasCuratedProfile);
            if (hasKnownConflict)
                return AppendNotice(formatted, Zh.IsActive()
                    ? (snapshot.SafetySourcesComplete
                        ? "当前效果与这条说明不一致。"
                        : "已识别的效果与这条说明不一致，且还有未确认的效果或作用条件。")
                    : (snapshot.SafetySourcesComplete
                        ? "Current effects conflict with this item-effect note."
                        : "Known effects conflict with this item-effect note, and other sources or conditions are unverified."));

            if (!snapshot.SafetySourcesComplete)
                return AppendNotice(formatted, Zh.IsActive()
                    ? (hasSafetyClaim && hasStateCheck
                        ? "部分效果来源或作用条件尚未确认，无法核实这条说明。"
                        : "部分物品效果或作用条件尚未确认，数值行可能不完整。")
                    : (hasSafetyClaim && hasStateCheck
                        ? "This effect note could not be checked against every effect source and target condition."
                        : "Some item effects or target conditions are unverified; modifier rows may be incomplete."));

            return formatted;
        }

        private static string FormatForState(string state, string baseDescription, string context,
            IDictionary<string, string> stateDescriptions, bool hasCuratedProfile)
        {
            string stateDescription = null;
            if (stateDescriptions != null) stateDescriptions.TryGetValue(state, out stateDescription);
            return FormatForState(state, baseDescription, context, stateDescription, hasCuratedProfile);
        }

        private static string FormatForState(string state, string baseDescription, string context,
            string stateDescription, bool hasCuratedProfile)
        {
            if (!hasCuratedProfile) return baseDescription;

            string stableContext = state == "raw" && string.IsNullOrWhiteSpace(context) ? baseDescription : context;
            if (string.IsNullOrWhiteSpace(stateDescription))
            {
                // Preserve legacy text for raw items. For an item with a curated profile, cooked
                // states show only its verified neutral context, never an unclassified raw claim.
                return state == "raw" ? baseDescription : context;
            }

            if (string.IsNullOrWhiteSpace(stableContext)) return stateDescription;
            return stableContext.TrimEnd() + " " + stateDescription.TrimStart();
        }

        public static string StateFor(Item item)
        {
            ItemCooking cooking = item != null ? item.cooking : null;
            if (cooking == null || cooking.timesCookedLocal <= 0) return "raw";
            if (cooking.wreckWhenCooked || IsExplicitlyUnusable(item)) return "unusable";
            if (cooking.timesCookedLocal >= 4) return "incinerated";
            if (cooking.timesCookedLocal == 3) return "burnt";
            return "cooked";
        }

        private static string AppendNotice(string description, string notice)
        {
            return string.IsNullOrWhiteSpace(description) ? notice : description.TrimEnd() + " " + notice;
        }

        private static bool IsExplicitlyUnusable(Item item)
        {
            if (item == null || OverrideUsabilityField == null) return false;

            object option = OverrideUsabilityField.GetValue(item);
            if (option == null || OverrideUsabilityIsNone == null || OverrideUsabilityValue == null) return false;
            if (!(ReadMember(option, OverrideUsabilityIsNone) is bool isNone) || isNone) return false;
            return ReadMember(option, OverrideUsabilityValue) is bool canUse && !canUse;
        }

        private static MemberInfo FindMember(Type type, string name)
        {
            if (type == null) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            return (MemberInfo)type.GetProperty(name, flags) ?? type.GetField(name, flags);
        }

        private static object ReadMember(object target, MemberInfo member)
        {
            if (target == null || member == null) return null;
            if (member is PropertyInfo property) return property.GetValue(target, null);
            if (member is FieldInfo field) return field.GetValue(target);
            return null;
        }
    }
}
