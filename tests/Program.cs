using System;
using System.IO;
using PeakItemTooltip;
using Newtonsoft.Json.Linq;
using UnityEngine;

static class Program
{
    private static int checks;

    static void Main()
    {
        var states = new System.Collections.Generic.Dictionary<string, string>
        {
            ["raw"] = "Raw poison starts later.",
            ["cooked"] = "Removes poison and grants stamina.",
            ["burnt"] = "Stamina is gone.",
            ["incinerated"] = "Poison returns.",
            ["unusable"] = "Destroyed."
        };
        var cookedPoisonCheck = new System.Collections.Generic.Dictionary<string, StateCheck>
        {
            ["cooked"] = new StateCheck { MustNotHaveActive = new System.Collections.Generic.List<string> { "Poison" } }
        };

        // The same instance is refreshed as the game mutates it during cooking.
        var berry = new Item();
        var hunger = berry.Add(new Action_RestoreHunger { restorationAmount = .2f });
        var poison = berry.Add(new Action_InflictPoison { poisonPerSecond = .05f, inflictionTime = 2f });
        Has(ItemModifiers.Format(berry), "-20 Hunger", "raw hunger");
        Has(ItemModifiers.Format(berry), "+10 Poison", "raw poison");
        Equal(ItemDescription.Format(berry, "Found here.", "Found here.", states), "Found here. Raw poison starts later.", "raw context and state description");

        berry.cooking = new ItemCooking { timesCookedLocal = 1 };
        hunger.restorationAmount = .4f;
        poison.enabled = false;
        var stamina = berry.Add(new Action_GiveExtraStamina { amount = .1f });
        Has(ItemModifiers.Format(berry), "-40 Hunger", "cooking uses mutated hunger");
        Has(ItemModifiers.Format(berry), "+10 Extra Stamina", "cooking adds stamina");
        Lacks(ItemModifiers.Format(berry), "Poison", "cooking disables poison");
        string onceCookedDescription = ItemDescription.Format(berry, "Raw warning", "Found here.", states);
        Equal(onceCookedDescription, "Found here. Removes poison and grants stamina.", "cooked description uses current state, not stale raw warning or count");
        Lacks(onceCookedDescription, "Raw warning", "cooked description suppresses stale raw warning");
        Equal(ItemDescription.Format(new Item { cooking = new ItemCooking { timesCookedLocal = 1 } }, "Raw warning", "", states),
            "Removes poison and grants stamina.", "cooked description without neutral context never falls back to raw warning");
        berry.cooking.timesCookedLocal = 2;
        Equal(ItemDescription.Format(berry, "Raw warning", "Found here.", states), onceCookedDescription, "cooked stages with identical effects use identical description");

        var refresh = TooltipItemSnapshot.Capture(berry);
        berry.cooking.timesCookedLocal = 4;
        hunger.restorationAmount = .3f;
        Equal(ItemDescription.FormatSnapshot(refresh, "Raw warning", "Found here.", states, cookedPoisonCheck), onceCookedDescription, "description uses the captured cooking state");
        Has(ItemModifiers.FormatSnapshot(refresh), "-40 Hunger", "numeric rows use the same captured refresh");

        stamina.amount = 0f;
        berry.Add(new Action_ModifyStatus { statusType = CharacterAfflictions.STATUSTYPE.Poison, changeAmount = .1f });
        Has(ItemModifiers.Format(berry), "+10 Poison", "burnt poison remains visible");
        Lacks(ItemModifiers.Format(berry), "Extra Stamina", "burnt zero stamina is omitted");
        Equal(ItemDescription.Format(berry, "Raw warning", "Found here.", states), "Found here. Poison returns.", "overcooking selects its own description");

        var wrecked = new Item { cooking = new ItemCooking { timesCookedLocal = 1, wreckWhenCooked = true } };
        Equal(ItemDescription.Format(wrecked, "Raw warning", "Found here.", states), "Found here. Destroyed.", "wrecked item does not retain raw or cooked benefits");
        var behaviorWrecked = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        behaviorWrecked.SetUsabilityOverride(false);
        Equal(ItemDescription.StateFor(behaviorWrecked), "unusable", "unusable override from CookingBehavior_Wreck selects unusable state");
        behaviorWrecked.SetUsabilityOverride(true);
        Equal(ItemDescription.StateFor(behaviorWrecked), "cooked", "explicit usable override retains cooked state");

        Equal(ItemDescription.StateFor(new Item { cooking = new ItemCooking() }), "raw", "zero cook count selects raw state");
        Equal(ItemDescription.StateFor(new Item { cooking = new ItemCooking { timesCookedLocal = 1 } }), "cooked", "first cook selects cooked state");
        Equal(ItemDescription.StateFor(new Item { cooking = new ItemCooking { timesCookedLocal = 2 } }), "cooked", "second cook shares cooked state");
        Equal(ItemDescription.StateFor(new Item { cooking = new ItemCooking { timesCookedLocal = 3 } }), "burnt", "third cook selects burnt state");
        Equal(ItemDescription.StateFor(new Item { cooking = new ItemCooking { timesCookedLocal = 4 } }), "incinerated", "fourth cook selects incinerated state");

        // Not all foods lose poison when cooked: obey the live component, not a blanket rule.
        var stillPoisonous = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        stillPoisonous.Add(new Action_InflictPoison { poisonPerSecond = .05f, inflictionTime = 2f });
        Has(ItemModifiers.Format(stillPoisonous), "+10 Poison", "enabled cooked poison remains visible");
        var conflictingSnapshot = TooltipItemSnapshot.Capture(stillPoisonous);
        const string completeBerryDescription = "Found on bushes. Raw food causes Poison; cooked stages remove its original Poison; incineration can add Poison again.";
        for (int cooks = 0; cooks <= 4; cooks++)
            Equal(ItemDescription.Format(new Item { cooking = new ItemCooking { timesCookedLocal = cooks } },
                completeBerryDescription, "", null), completeBerryDescription,
                "complete conditional prose stays available at cooking stage " + cooks);
        Equal(ItemDescription.Format(wrecked, completeBerryDescription, "", null), completeBerryDescription,
            "complete conditional prose stays available on an unusable item");
        Equal(ItemDescription.FormatSnapshot(conflictingSnapshot, completeBerryDescription, "", null, cookedPoisonCheck),
            completeBerryDescription + " Current effects conflict with this item-effect note.",
            "shared prose keeps a live Poison conflict notice even without a state-specific sentence");
        Equal(ItemDescription.FormatSnapshot(conflictingSnapshot, "Raw poison", "Found here.",
            new System.Collections.Generic.Dictionary<string, string> { ["cooked"] = "Cooking removes poison." }, cookedPoisonCheck),
            "Found here. Current effects conflict with this item-effect note.", "a Wiki safety claim is suppressed when live poison remains active");

        var coveredConsumable = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        coveredConsumable.Add(new Action_Consume());
        Equal(ItemDescription.FormatSnapshot(TooltipItemSnapshot.Capture(coveredConsumable), "Raw text", "Found here.",
            new System.Collections.Generic.Dictionary<string, string> { ["cooked"] = "Cooking removes poison." }, cookedPoisonCheck),
            "Found here. Cooking removes poison.", "known consumption action does not suppress a verified safety claim");

        var afflictionItem = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        afflictionItem.Add(new Action_ApplyAffliction { affliction = new Peak.Afflictions.UnknownAffliction() });
        var afflictionSnapshot = TooltipItemSnapshot.Capture(afflictionItem);
        Equal(afflictionSnapshot.SafetySourcesComplete, false, "unparsed affliction action marks safety coverage incomplete");
        Equal(ItemDescription.FormatSnapshot(afflictionSnapshot, completeBerryDescription, "", null, cookedPoisonCheck),
            completeBerryDescription + " Some item effects or target conditions are unverified; modifier rows may be incomplete.",
            "shared prose discloses incomplete numeric coverage instead of treating unknown effects as absent");
        Equal(ItemDescription.FormatSnapshot(afflictionSnapshot, "Raw text", "Found here.",
            new System.Collections.Generic.Dictionary<string, string> { ["cooked"] = "Cooking removes poison." }, cookedPoisonCheck),
            "Found here. This effect note could not be checked against every effect source and target condition.",
            "unknown affliction cannot produce a false safety claim");
        Equal(ItemDescription.FormatSnapshot(afflictionSnapshot, "Raw text", "Found here.",
            new System.Collections.Generic.Dictionary<string, string> { ["cooked"] = "Cooking changes the effect." }, null),
            "Found here. Cooking changes the effect. Some item effects or target conditions are unverified; modifier rows may be incomplete.",
            "unparsed effects are disclosed even without a safety claim");

        var parsedAffliction = new Item();
        parsedAffliction.Add(new Action_ApplyAffliction
        {
            affliction = new Peak.Afflictions.Affliction_AdjustStatus
            {
                statusType = CharacterAfflictions.STATUSTYPE.Injury,
                statusAmount = .05f
            },
            extraAfflictions = new Peak.Afflictions.Affliction[]
            {
                new Peak.Afflictions.Affliction_PoisonOverTime
                {
                    totalTime = 6f,
                    delayBeforeEffect = 2f,
                    statusPerSecond = .025f
                }
            }
        });
        Has(ItemModifiers.Format(parsedAffliction), "+5 Injury", "ApplyAffliction status payload is shown in modifier rows");
        Has(ItemModifiers.Format(parsedAffliction), "+10 Poison (after 2s delay, over 4s)", "extra timed poison affliction includes amount and timing");
        Has(string.Join(",", ItemModifiers.ActivePositiveStatuses(parsedAffliction)), "Poison", "parsed positive affliction participates in safety checks");
        Equal(TooltipItemSnapshot.Capture(parsedAffliction).SafetySourcesComplete, true,
            "fully parsed direct and extra afflictions count as supported sources");

        var triggeredAffliction = new Item();
        triggeredAffliction.Add(new Action_ApplyAffliction
        {
            OnSecondaryHeld = true,
            affliction = new Peak.Afflictions.Affliction_AdjustStatus
            {
                statusType = CharacterAfflictions.STATUSTYPE.Cold,
                statusAmount = .05f
            }
        });
        Has(ItemModifiers.Format(triggeredAffliction), "+5 Cold [while secondary held]",
            "affliction-backed modifier also displays its action trigger");

        var extendedAfflictions = new Item();
        extendedAfflictions.Add(new Action_ApplyAffliction
        {
            affliction = new Peak.Afflictions.Affliction_AdjustColdOverTime
            {
                statusPerSecond = -.02f,
                totalTime = 5f
            },
            extraAfflictions = new Peak.Afflictions.Affliction[]
            {
                new Peak.Afflictions.Affliction_AdjustDrowsyOverTime
                {
                    statusPerSecond = .01f,
                    totalTime = 4f
                },
                new Peak.Afflictions.Affliction_AddBonusStamina
                {
                    staminaAmount = .1f,
                    totalTime = 6f
                }
            }
        });
        Has(ItemModifiers.Format(extendedAfflictions), "-10 Cold (over 5s)", "cold affliction shows signed aggregate and duration");
        Has(ItemModifiers.Format(extendedAfflictions), "+4 Drowsy (over 4s)", "drowsy affliction shows signed aggregate and duration");
        Has(ItemModifiers.Format(extendedAfflictions), "+10 Extra Stamina (over 6s)", "bonus stamina affliction shows amount and duration");
        Equal(TooltipItemSnapshot.Capture(extendedAfflictions).SafetySourcesComplete, true,
            "supported cold, drowsy, and stamina afflictions count as covered");
        Has(string.Join(",", ItemModifiers.ActivePositiveStatuses(extendedAfflictions)), "Drowsy",
            "positive timed drowsy affliction participates in safety checks");
        Has(string.Join(",", ItemModifiers.ActivePositiveStatuses(extendedAfflictions)), "ExtraStamina",
            "bonus stamina affliction participates in safety checks");

        var inheritedAffliction = new Item();
        inheritedAffliction.Add(new Peak.Action_SuperJumpAmulet
        {
            affliction = new Peak.Afflictions.Affliction_AdjustStatus
            {
                statusType = CharacterAfflictions.STATUSTYPE.Poison,
                statusAmount = .1f
            },
            petrifyPerUse = .05f
        });
        Has(ItemModifiers.Format(inheritedAffliction), "+10 Poison", "derived Action_ApplyAffliction payload is rendered");
        Has(ItemModifiers.Format(inheritedAffliction), "+5 Petrify (per use)", "derived action petrify modifier is rendered");
        Has(string.Join(",", ItemModifiers.ActivePositiveStatuses(inheritedAffliction)), "Petrify",
            "petrify field participates in safety checks");
        Equal(TooltipItemSnapshot.Capture(inheritedAffliction).SafetySourcesComplete, true,
            "known super-jump affliction subclass is covered when its payload is supported");

        var removedStatusAffliction = new Item();
        removedStatusAffliction.Add(new Action_ApplyAffliction
        {
            affliction = new Peak.Afflictions.Affliction_AdjustStatus
            {
                statusType = CharacterAfflictions.STATUSTYPE.Poison,
                statusAmount = -.2f
            }
        });
        Has(ItemModifiers.Format(removedStatusAffliction), "-20 Poison", "negative ApplyAffliction status remains signed as removal");
        Lacks(string.Join(",", ItemModifiers.ActivePositiveStatuses(removedStatusAffliction)), "Poison",
            "negative ApplyAffliction status does not count as a positive risk");

        var massAffliction = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        var massAction = massAffliction.Add(new Action_ApplyMassAffliction
        {
            radius = 4.8f,
            ignoreCaster = true,
            affliction = new Peak.Afflictions.Affliction_AdjustStatus
            {
                statusType = CharacterAfflictions.STATUSTYPE.Poison,
                statusAmount = .1f
            }
        });
        var massSnapshot = TooltipItemSnapshot.Capture(massAffliction);
        Has(massSnapshot.Modifiers, "+10 Poison", "area payload amount is shown");
        Has(massSnapshot.Modifiers, "characters within 4.8m of item", "area row identifies the item-centered radius");
        Has(massSnapshot.Modifiers, "holder excluded", "area row states the holder exclusion");
        Lacks(string.Join(",", massSnapshot.ActivePositiveStatuses), "Poison", "area Poison is not an unconditional self status");
        massAction.radius = 2.125f;
        massAction.ignoreCaster = false;
        massAction.extraAfflictions = new Peak.Afflictions.Affliction[]
        {
            new Peak.Afflictions.Affliction_AddBonusStamina { staminaAmount = .2f, totalTime = 6f }
        };
        Has(ItemModifiers.Format(massAffliction), "characters within 2.125m of item", "area radius follows the live action value");
        Lacks(ItemModifiers.Format(massAffliction), "holder excluded", "changed area eligibility removes the old qualifier");
        Has(ItemModifiers.Format(massAffliction), "+20 Extra Stamina (over 6s) [characters within 2.125m of item on application]",
            "extra affliction inherits the same area target condition");
        Has(massSnapshot.Modifiers, "characters within 4.8m of item", "captured area rows do not change with a later mutation");
        massAction.radius = -1f;
        Equal(ItemModifiers.Format(massAffliction), "", "negative radius cannot produce a reachable area effect");
        massAction.radius = float.NaN;
        Equal(ItemModifiers.Format(massAffliction), "", "invalid radius does not emit an unqualified area row");
        massAction.radius = 4.8f;
        massAction.affliction = null;
        Equal(ItemModifiers.Format(massAffliction), "", "null primary area payload prevents extras just as the game does");
        massAction.affliction = new Peak.Afflictions.Affliction_AdjustStatus
            { statusType = CharacterAfflictions.STATUSTYPE.Poison, statusAmount = .1f };
        massAction.enabled = false;
        Equal(ItemModifiers.Format(massAffliction), "", "disabled area source contributes no rows");
        Zh.Active = true;
        massAction.enabled = true;
        massAction.ignoreCaster = true;
        Has(ItemModifiers.Format(massAffliction), "物品周围 4.8 米内的玩家、排除持有者", "area conditions are localized");
        Zh.Active = false;
        Equal(massSnapshot.SafetySourcesComplete, false,
            "area eligibility is not evaluated as an unconditional self safety guarantee");
        Equal(ItemDescription.FormatSnapshot(massSnapshot, "Raw text", "Found here.",
            new System.Collections.Generic.Dictionary<string, string> { ["cooked"] = "Cooking removes poison." }, cookedPoisonCheck),
            "Found here. This effect note could not be checked against every effect source and target condition.",
            "mass action cannot bypass the safety-claim fallback");

        var milk = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        var invincibility = new Peak.Afflictions.Affliction_Invincibility { totalTime = 10f };
        var milkAction = milk.Add(new Action_ApplyAffliction { affliction = invincibility });
        Has(ItemModifiers.Format(milk), "Invincibility (duration 10s)", "buff duration reads the actual payload");
        var milkSnapshot = TooltipItemSnapshot.Capture(milk);
        invincibility.totalTime = 20f;
        Has(ItemModifiers.Format(milk), "Invincibility (duration 20s)", "cooking-mutated buff duration refreshes");
        Has(milkSnapshot.Modifiers, "duration 10s", "buff snapshot retains its original duration");
        Equal(ItemModifiers.HasCompleteSafetyCoverage(milk), true, "known pure buff is fully accounted for");
        milkAction.extraAfflictions = new Peak.Afflictions.Affliction[]
        {
            new Peak.Afflictions.Affliction_Sunscreen { totalTime = 90f },
            new Peak.Afflictions.Affliction_Numb { totalTime = 60f },
            new Peak.Afflictions.Affliction_Blind { totalTime = 2f },
            new Peak.Afflictions.Affliction_Glowing { totalTime = 30f }
        };
        Has(ItemModifiers.Format(milk), "Sun Protection (duration 90s)", "sun protection extra payload renders");
        Has(ItemModifiers.Format(milk), "Numb (Stamina bar hidden) (duration 60s)", "numb extra payload explains its effect");
        Has(ItemModifiers.Format(milk), "Blind (duration 2s)", "blind extra payload renders");
        Has(ItemModifiers.Format(milk), "Glowing (duration 30s)", "glowing extra payload renders");
        invincibility.totalTime = float.NaN;
        Lacks(ItemModifiers.Format(milk), "Invincibility", "invalid buff duration is not displayed as a promise");
        Equal(ItemModifiers.HasCompleteSafetyCoverage(milk), false, "invalid buff data retains incomplete-coverage notice");
        invincibility.totalTime = 20f;
        milkAction.enabled = false;
        Equal(ItemModifiers.Format(milk), "", "disabled timed buffs are omitted");
        milkAction.enabled = true;

        var lollipop = new Item();
        var endDrowsy = new Peak.Afflictions.Affliction_AdjustDrowsyOverTime { totalTime = 8f, statusPerSecond = .025f };
        var infinite = new Peak.Afflictions.Affliction_InfiniteStamina
            { totalTime = 6f, climbDelay = 4f, drowsyAffliction = endDrowsy };
        var lollipopAction = lollipop.Add(new Action_ApplyAffliction { affliction = infinite });
        Has(ItemModifiers.Format(lollipop), "Infinite Stamina (base 6s, up to 4s grace before climbing)", "infinite stamina preserves its conditional grace time");
        Has(ItemModifiers.Format(lollipop), "+20 Drowsy (over 8s) (after Infinite Stamina ends)", "end-of-buff penalty is separately qualified");
        Has(string.Join(",", ItemModifiers.ActivePositiveStatuses(lollipop)), "Drowsy", "known end penalty participates in risk detection");
        Equal(ItemModifiers.HasCompleteSafetyCoverage(lollipop), true, "known infinite-stamina and end payload are accounted for");
        infinite.totalTime = 12f;
        infinite.climbDelay = 0f;
        infinite.drowsyAffliction = null;
        Has(ItemModifiers.Format(lollipop), "Infinite Stamina (base 12s)", "changed infinite duration refreshes");
        Lacks(ItemModifiers.Format(lollipop), "Drowsy", "removing the end penalty removes its numeric row");
        Lacks(string.Join(",", ItemModifiers.ActivePositiveStatuses(lollipop)), "Drowsy", "removed end penalty no longer counts as risk");
        infinite.drowsyAffliction = infinite;
        Has(ItemModifiers.Format(lollipop), "Infinite Stamina (base 12s)", "unsupported or cyclic end payload does not hide the known primary buff");
        Equal(ItemModifiers.HasCompleteSafetyCoverage(lollipop), false, "unsupported end payload remains incomplete without recursion");
        infinite.drowsyAffliction = endDrowsy;
        endDrowsy.statusPerSecond = -.025f;
        Has(ItemModifiers.Format(lollipop), "-20 Drowsy", "negative end payload stays a reduction");
        Lacks(string.Join(",", ItemModifiers.ActivePositiveStatuses(lollipop)), "Drowsy", "negative end payload is not a positive risk");
        Zh.Active = true;
        Has(ItemModifiers.Format(lollipop), "无限耐力（基础时长 12 秒）", "infinite-stamina duration is localized");
        Has(ItemModifiers.Format(lollipop), "无限耐力结束后", "end-payload timing is localized");
        Has(ItemModifiers.Format(milk), "无敌（持续 20 秒）", "simple buff duration is localized");
        Zh.Active = false;
        massAction.affliction = infinite;
        Has(ItemModifiers.Format(massAffliction), "Infinite Stamina (base 12s) [characters within 4.8m of item on application, holder excluded]",
            "area buff retains initial target eligibility");
        Has(ItemModifiers.Format(massAffliction), "after Infinite Stamina ends) [characters within 4.8m of item on application, holder excluded]",
            "area end penalty belongs to the initial target, not a new area hit");

        var directInfiniteItem = new Item();
        var directInfinite = directInfiniteItem.Add(new Action_ApplyInfiniteStamina { buffTime = 5f, drowsyAmount = 99f });
        Has(ItemModifiers.Format(directInfiniteItem), "Infinite Stamina (duration 5s)", "direct infinite-stamina action reads buffTime");
        Lacks(ItemModifiers.Format(directInfiniteItem), "Drowsy", "unused serialized drowsyAmount is not reported as an effect");
        Equal(ItemModifiers.HasCompleteSafetyCoverage(directInfiniteItem), true, "verified direct infinite action is accounted for");
        directInfinite.buffTime = 10f;
        Has(ItemModifiers.Format(directInfiniteItem), "duration 10s", "direct infinite duration follows the live component");
        directInfinite.enabled = false;
        Equal(ItemModifiers.Format(directInfiniteItem), "", "disabled direct infinite action contributes no buff");

        var healingItem = new Item();
        var healing = new Peak.Afflictions.Affliction_HealAll { maxHealing = .35f };
        var healingAction = healingItem.Add(new Action_ApplyAffliction { affliction = healing });
        Has(ItemModifiers.Format(healingItem), "Heal up to 35 points total (order: Injury > Spores > Poison > Cold > Hot > Drowsy)",
            "healing shares one bounded budget across the verified status order");
        Lacks(ItemModifiers.Format(healingItem), "-35 Poison", "shared budget is not promised independently for poison");
        Equal(ItemModifiers.HasCompleteSafetyCoverage(healingItem), true, "valid healing is reduction only");
        var healingSnapshot = TooltipItemSnapshot.Capture(healingItem);
        healing.maxHealing = .7f;
        Has(ItemModifiers.Format(healingItem), "70 points total", "live healing budget refreshes");
        Has(healingSnapshot.Modifiers, "35 points total", "snapshot preserves old budget");
        healing.maxHealing = float.NaN;
        Equal(ItemModifiers.Format(healingItem), "", "invalid healing budget is omitted");
        Equal(ItemModifiers.HasCompleteSafetyCoverage(healingItem), false, "invalid budget is not trusted");
        healing.maxHealing = -.1f;
        Equal(ItemModifiers.Format(healingItem), "", "negative budget is not presented as healing");
        healing.maxHealing = .35f;
        healingAction.enabled = false;
        Equal(ItemModifiers.Format(healingItem), "", "disabled healing is omitted");
        healingAction.enabled = true;
        var clear = new Peak.Afflictions.Affliction_ClearAllStatus { excludeCurse = true };
        healingAction.affliction = clear;
        Has(ItemModifiers.Format(healingItem), "excludes: Crab, Weight, Thorns, Arrow, Curse, Petrify", "clear lists fixed and configured exclusions");
        Equal(ItemModifiers.HasCompleteSafetyCoverage(healingItem), true, "clear payload is reduction only");
        clear.excludeCurse = false;
        clear.includePetrify = true;
        Has(ItemModifiers.Format(healingItem), "excludes: Crab, Weight, Thorns, Arrow)", "live flags remove optional exclusions");
        Lacks(ItemModifiers.Format(healingItem), "Curse", "included curse is no longer excluded");
        Lacks(ItemModifiers.Format(healingItem), "Petrify", "included petrify is no longer excluded");
        var exhaustion = new Peak.Afflictions.Affliction_Exhaustion { totalTime = 5f, drainAmount = .2f };
        healingAction.affliction = exhaustion;
        Has(ItemModifiers.Format(healingItem), "Base Stamina drain 20 points (over 5s; scaled by ascent difficulty, bypassed by Infinite Stamina)",
            "drain is conditional rather than guaranteed observed loss");
        Equal(ItemModifiers.HasCompleteSafetyCoverage(healingItem), false, "resource loss retains conservative notice");
        exhaustion.drainAmount = .4f;
        Has(ItemModifiers.Format(healingItem), "drain 40 points", "live drain amount refreshes");
        exhaustion.totalTime = 0f;
        Equal(ItemModifiers.Format(healingItem), "", "zero duration does not divide by zero");
        exhaustion.totalTime = 5f;
        exhaustion.drainAmount = float.PositiveInfinity;
        Equal(ItemModifiers.Format(healingItem), "", "nonfinite drain is omitted");
        healingAction.affliction = new Peak.Afflictions.Affliction_NoHunger { totalTime = 45f };
        healingAction.extraAfflictions = new Peak.Afflictions.Affliction[] { new Peak.Afflictions.Affliction_BingBongShield { totalTime = 10f } };
        Has(ItemModifiers.Format(healingItem), "No Hunger (blocks gain, does not clear existing Hunger) (duration 45s)", "prevention is not restoration");
        Has(ItemModifiers.Format(healingItem), "Bing Bong Shield (Invincibility) (duration 10s)", "shield duration reads extra payload");
        Equal(ItemModifiers.HasCompleteSafetyCoverage(healingItem), true, "protection payloads are accounted for");
        Zh.Active = true;
        Has(ItemModifiers.Format(healingItem), "免饥饿（阻止增加，不清除已有饥饿）", "hunger prevention is localized");
        healingAction.affliction = healing;
        Has(ItemModifiers.Format(healingItem), "最多共恢复 35 点", "shared budget is localized");
        healingAction.affliction = clear;
        Has(ItemModifiers.Format(healingItem), "清除可治愈状态", "clear exclusions are localized");
        Zh.Active = false;
        massAction.affliction = healing;
        Has(ItemModifiers.Format(massAffliction), "points total (order:", "area healing retains shared budget");
        Has(ItemModifiers.Format(massAffliction), "holder excluded", "area healing retains target conditions");
        Equal(ItemModifiers.HasCompleteSafetyCoverage(massAffliction), false, "area healing is not unconditional self safety");

        var conditionalItem = new Item();
        var bite = new Peak.Afflictions.Affliction_ZombieBite { totalTime = 12f, delayBeforeEffect = 2f, statusPerSecond = .01f };
        var conditionalAction = conditionalItem.Add(new Action_ApplyAffliction { affliction = bite });
        Has(ItemModifiers.Format(conditionalItem), "Up to 10 Spores (after 2s delay, over 10s; ends if Spores falls below 2.5 points)",
            "bite dose is an upper bound with its early termination condition");
        Equal(ItemModifiers.HasCompleteSafetyCoverage(conditionalItem), false, "conditional spores is not treated as absent");
        bite.delayBeforeEffect = 7f;
        Has(ItemModifiers.Format(conditionalItem), "Up to 5 Spores", "bite duration excludes mutated delay");
        bite.statusPerSecond = float.NaN;
        Equal(ItemModifiers.Format(conditionalItem), "", "invalid bite rate is omitted");
        var aura = new Peak.Afflictions.Affliction_RadiateInfiniteStam { totalTime = 20f, radius = 4.25f };
        conditionalAction.affliction = aura;
        Has(ItemModifiers.Format(conditionalItem), "of recipient, including self", "aura is centered on recipient rather than the item");
        Has(ItemModifiers.Format(conditionalItem), "within 4.25m", "aura range retains target-condition precision");
        Has(ItemModifiers.Format(conditionalItem), "refreshes a 1s buff every 0.5s", "aura does not promise whole-duration buff to players leaving range");
        Equal(ItemModifiers.HasCompleteSafetyCoverage(conditionalItem), false, "aura target condition remains conservative");
        aura.radius = -1f;
        Equal(ItemModifiers.Format(conditionalItem), "", "invalid aura radius is omitted");
        var chaos = new Peak.Afflictions.Affliction_Chaos { statusAmountAverage = .5f, statusAmountStandardDeviation = .1f };
        conditionalAction.affliction = chaos;
        Has(ItemModifiers.Format(conditionalItem), "randomly replace Extra Stamina; outcome unknown", "chaos resets stamina instead of promising an additive gain");
        Lacks(ItemModifiers.Format(conditionalItem), "+50", "distribution mean is not rendered as guaranteed amount");
        Equal(ItemModifiers.HasCompleteSafetyCoverage(conditionalItem), false, "random outcome cannot validate a no-poison claim");
        chaos.statusAmountStandardDeviation = float.NaN;
        Equal(ItemModifiers.Format(conditionalItem), "", "invalid random parameters retain incomplete coverage");
        chaos.statusAmountStandardDeviation = .1f;
        conditionalAction.enabled = false;
        Equal(ItemModifiers.Format(conditionalItem), "", "disabled random source is omitted");
        conditionalAction.enabled = true;
        Zh.Active = true;
        Has(ItemModifiers.Format(conditionalItem), "随机重设额外耐力", "random replacement semantics are localized");
        conditionalAction.affliction = bite;
        bite.statusPerSecond = .01f;
        Has(ItemModifiers.Format(conditionalItem), "孢子低于 2.5 点时终止", "early termination condition is localized");
        Zh.Active = false;

        var randomMushroom = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        randomMushroom.Add(new Action_RandomMushroomEffect());
        Equal(TooltipItemSnapshot.Capture(randomMushroom).SafetySourcesComplete, false,
            "random mushroom action marks safety coverage incomplete");

        var customAction = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        customAction.Add(new CustomUnknownAction());
        Equal(TooltipItemSnapshot.Capture(customAction).SafetySourcesComplete, false,
            "unrecognized ItemActionBase subclass marks safety coverage incomplete");

        var disabledUnknownAction = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        disabledUnknownAction.Add(new Action_ApplyAffliction { enabled = false });
        Equal(TooltipItemSnapshot.Capture(disabledUnknownAction).SafetySourcesComplete, true,
            "disabled unknown action does not affect safety coverage");

        var conditionalStatus = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        conditionalStatus.Add(new Action_ModifyStatus
        {
            statusType = CharacterAfflictions.STATUSTYPE.Poison,
            changeAmount = .1f,
            ifSkeleton = true,
            OnConsumed = true
        });
        Has(ItemModifiers.Format(conditionalStatus), "+10 Poison [skeleton only]",
            "skeleton-only status row discloses its character condition");
        Equal(TooltipItemSnapshot.Capture(conditionalStatus).SafetySourcesComplete, false,
            "character-conditional status action marks safety coverage incomplete");

        var triggeredPoison = new Item();
        triggeredPoison.Add(new Action_InflictPoison
        {
            poisonPerSecond = .05f,
            inflictionTime = 2f,
            OnPressed = true
        });
        Has(ItemModifiers.Format(triggeredPoison), "+10 Poison (2s) [on press]",
            "non-consumption trigger is shown beside the modifier");

        var consumedPoison = new Item();
        consumedPoison.Add(new Action_InflictPoison
        {
            poisonPerSecond = .05f,
            inflictionTime = 2f,
            OnConsumed = true
        });
        Has(ItemModifiers.Format(consumedPoison), "+10 Poison",
            "ordinary consumed effects keep a concise modifier label");
        Lacks(ItemModifiers.Format(consumedPoison), "[on consume]",
            "ordinary consume trigger is not needlessly repeated");

        Zh.Active = true;
        var chineseConditional = new Item();
        chineseConditional.Add(new Action_ModifyStatus
        {
            statusType = CharacterAfflictions.STATUSTYPE.Poison,
            changeAmount = .1f,
            ifSkeleton = true,
            OnPressed = true,
            OnConsumed = true
        });
        Has(ItemModifiers.Format(chineseConditional), "+10 中毒【仅骷髅、按下时、消耗时】",
            "Chinese modifier labels preserve both character condition and action triggers");
        Zh.Active = false;

        var disabled = new Item();
        disabled.Add(new Action_RestoreHunger { restorationAmount = .2f, enabled = false });
        disabled.Add(new Action_InflictPoison { poisonPerSecond = .05f, inflictionTime = 2f, enabled = false });
        disabled.Add(new Action_ModifyStatus { statusType = CharacterAfflictions.STATUSTYPE.Weight, changeAmount = .1f, enabled = false });
        disabled.Add(new Action_GiveExtraStamina { amount = .1f, enabled = false });
        disabled.Add(new PetrifySource { petrifyPerJump = 10, enabled = false });
        disabled.Add(new Peak.Action_HealingGem { minPetrify = .1f, maxPetrify = .2f, enabled = false });
        Equal(ItemModifiers.Format(disabled), "", "all disabled effect families omitted");

        var hierarchy = new Item();
        var parent = new GameObject(hierarchy.transform);
        var child = new GameObject(parent.transform);
        hierarchy.Add(new Action_RestoreHunger { restorationAmount = .2f }, child.transform);
        Has(ItemModifiers.Format(hierarchy), "-20 Hunger", "active nested branch");
        parent.activeSelf = false;
        Equal(ItemModifiers.Format(hierarchy), "", "inactive ancestor suppresses child effect");
        parent.activeSelf = true;
        child.activeSelf = false;
        Equal(ItemModifiers.Format(hierarchy), "", "inactive child suppresses effect");
        child.activeSelf = true;
        hierarchy.transform.gameObject.activeSelf = false;
        Has(ItemModifiers.Format(hierarchy), "-20 Hunger", "inactive inventory root retains valid effects");

        var duration = new Item();
        var timed = duration.Add(new Action_InflictPoison { poisonPerSecond = .05f, inflictionTime = 0f });
        Equal(ItemModifiers.Format(duration), "", "zero poison duration");
        timed.inflictionTime = -1f;
        Equal(ItemModifiers.Format(duration), "", "negative duration does not become poison recovery");
        timed.inflictionTime = 4f;
        Has(ItemModifiers.Format(duration), "+20 Poison", "live poison duration");

        var amulet = new Item();
        amulet.Add(new PetrifySource { petrifyPerJump = 10 });
        amulet.Add(new Peak.Action_HealingGem { minPetrify = .1f, maxPetrify = .2f });
        Has(ItemModifiers.Format(amulet), "+10 Petrify (per jump)", "enabled petrify unchanged");
        Has(ItemModifiers.Format(amulet), "+10–20 Petrify", "enabled healing gem unchanged");

        var thornItem = new Item();
        thornItem.Add(new Action_AddOrRemoveThorns { thornCount = 3 });
        Has(ItemModifiers.Format(thornItem), "+3 Thorns", "direct thorn action is included in modifier rows");
        Has(string.Join(",", ItemModifiers.ActivePositiveStatuses(thornItem)), "Thorns", "direct thorn action participates in safety checks");
        thornItem.components.Clear();
        thornItem.Add(new Action_AddOrRemoveThorns { thornCount = -2 });
        Has(ItemModifiers.Format(thornItem), "-2 Thorns", "negative thorn action shows thorn removal");

        Zh.Active = true;
        Has(ItemModifiers.Format(berry), "+10 中毒", "localized live effects");
        Has(ItemModifiers.Format(parsedAffliction), "+10 中毒（延迟 2 秒，持续 4 秒）", "localized ApplyAffliction timing");
        Has(ItemModifiers.Format(extendedAfflictions), "-10 寒冷（持续 5 秒）", "localized timed cold affliction");
        Has(ItemDescription.Format(berry, "生食会中毒", "在雨林发现。", new System.Collections.Generic.Dictionary<string, string> { ["incinerated"] = "烧毁后再次中毒。" }), "烧毁后再次中毒", "localized state description");
        Equal(ItemDescription.Format(new Item { cooking = new ItemCooking() }, "自定义描述", "", states), "自定义描述 Raw poison starts later.", "custom raw description preserved with the selected state description");
        Equal(ItemDescription.Format(new Item { cooking = new ItemCooking { timesCookedLocal = 1 } }, "旧版条目描述", "", null), "旧版条目描述", "unreviewed item retains its legacy description");
        Equal(ItemDescription.Format(new Item { cooking = new ItemCooking { timesCookedLocal = 1 } }, "生食会中毒", "", new System.Collections.Generic.Dictionary<string, string> { ["raw"] = "Raw poison." }), "", "curated item without a cooked rule hides its unclassified raw warning");
        Equal(ItemModifiers.Format(null), "", "null item modifiers");
        Equal(ItemDescription.Format(null, "Fallback", "", states), "Fallback", "null item description");

        TestDescriptionDefaultsMerge();
        TestDescriptionDefaultsRestoreMissingSections();
        TestDescriptionsInitializeAndHotReload();

        Console.WriteLine($"PASS: {checks} regression checks");
    }

    private static void TestDescriptionDefaultsMerge()
    {
        JObject user = JObject.Parse(@"{
          'types': { 'CUSTOM': '#123456' },
          'items': {
            'green crispberry': {
              'type': 'CUSTOM',
              'description': 'User-authored legacy text',
              'customNote': 'keep this field',
              'context': '',
              'stateDescriptions': { 'raw': 'User raw text', 'cooked': '' },
              'stateChecks': {},
              'wiki': { 'title': 'User-selected source', 'customSource': 'manual' }
            },
            'My Custom Item': { 'description': 'Keep custom entry' }
          }
        }");
        JObject defaults = JObject.Parse(@"{
          'types': { 'CONSUMABLE': '#F2C43D' },
          'items': {
            'Green Crispberry': {
              'type': 'CONSUMABLE',
              'description': 'Default text must not replace user text',
              'context': 'Found in the rainforest.',
              'stateDescriptions': {
                'raw': 'Default raw text',
                'cooked': 'Default cooked text',
                'burnt': 'Default burnt text'
              },
              'stateChecks': { 'cooked': { 'mustNotHaveActive': ['Poison'] } },
              'wiki': { 'title': 'Green Crispberry', 'revision': 10035, 'gameVersion': '2.6.b' }
            },
            'Defaults Only Item': { 'description': 'New default entry' }
          }
        }");

        var added = DescriptionDefaultsMerger.MergeMissing(user, defaults);
        Equal(added.Count, 6, "migration reports all inserted fields and entries");
        Equal(user["types"]["CUSTOM"].ToString(), "#123456", "migration preserves custom type");
        Equal(user["types"]["CONSUMABLE"].ToString(), "#F2C43D", "migration adds missing type");
        Equal(user["items"]["green crispberry"]["type"].ToString(), "CUSTOM", "migration preserves user's type");
        Equal(user["items"]["green crispberry"]["description"].ToString(), "User-authored legacy text", "migration preserves legacy description");
        Equal(user["items"]["green crispberry"]["customNote"].ToString(), "keep this field", "migration preserves custom fields");
        Equal(user["items"]["green crispberry"]["context"].ToString(), "", "migration preserves explicit empty context");
        Equal(user["items"]["green crispberry"]["stateDescriptions"]["raw"].ToString(), "User raw text", "migration preserves edited state text");
        Equal(user["items"]["green crispberry"]["stateDescriptions"]["cooked"].ToString(), "", "migration preserves explicit empty state text");
        Equal(user["items"]["green crispberry"]["stateDescriptions"]["burnt"].ToString(), "Default burnt text", "migration adds missing state text");
        Equal(user["items"]["green crispberry"]["stateChecks"]["cooked"]["mustNotHaveActive"][0].ToString(), "Poison", "migration adds missing state checks");
        Equal(user["items"]["green crispberry"]["wiki"]["title"].ToString(), "User-selected source", "migration preserves existing source metadata");
        Equal(user["items"]["green crispberry"]["wiki"]["revision"].ToString(), "10035", "migration adds missing source metadata");
        Equal(user["items"]["green crispberry"]["wiki"]["customSource"].ToString(), "manual", "migration preserves custom source metadata");
        Equal(user["items"]["My Custom Item"]["description"].ToString(), "Keep custom entry", "migration preserves custom item");
        Equal(user["items"]["Defaults Only Item"]["description"].ToString(), "New default entry", "migration adds new default item");
    }

    private static void TestDescriptionDefaultsRestoreMissingSections()
    {
        JObject user = JObject.Parse(@"{ 'customRoot': true }");
        JObject defaults = JObject.Parse(@"{
          'types': { 'CONSUMABLE': '#F2C43D' },
          'items': { 'Default Item': { 'description': 'Default text' } }
        }");

        var added = DescriptionDefaultsMerger.MergeMissing(user, defaults);
        Equal(added.Count, 2, "migration reports missing root sections");
        Equal(user["types"]["CONSUMABLE"].ToString(), "#F2C43D", "migration restores missing types section");
        Equal(user["items"]["Default Item"]["description"].ToString(), "Default text", "migration restores missing items section");
        Equal(user["customRoot"].ToString(), "True", "migration preserves unrelated root fields");
    }

    private static void TestDescriptionsInitializeAndHotReload()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "peak-item-tooltip-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            Zh.Active = false;
            BepInEx.Paths.ConfigPath = Path.Combine(tempRoot, "fresh-en");
            Directory.CreateDirectory(BepInEx.Paths.ConfigPath);
            var freshEnglish = new Descriptions();
            freshEnglish.Initialize();
            Equal(File.Exists(freshEnglish.Path_), true, "first initialization creates English defaults");
            Equal(freshEnglish.TryGet("Green Crispberry", out ItemInfo freshEnInfo), true, "fresh English defaults load");
            Equal(freshEnInfo.Description, "Default English raw description.", "fresh English description loads");

            BepInEx.Paths.ConfigPath = Path.Combine(tempRoot, "legacy-en");
            Directory.CreateDirectory(BepInEx.Paths.ConfigPath);
            File.WriteAllText(Path.Combine(BepInEx.Paths.ConfigPath, Descriptions.FileNameEn),
                "{\"types\":{\"CUSTOM\":\"#123456\"},\"items\":{\"green crispberry\":{\"type\":\"CUSTOM\",\"description\":\"My custom legacy description\"}}}");
            var migratedEnglish = new Descriptions();
            migratedEnglish.Initialize();
            Equal(migratedEnglish.TryGet("GREEN CRISPBERRY", out ItemInfo migratedEnInfo), true, "legacy English entry loads case-insensitively");
            Equal(migratedEnInfo.Description, "My custom legacy description", "legacy English description survives initialization");
            Equal(migratedEnInfo.Context, "Found in the rainforest.", "migration adds missing English context");
            Equal(migratedEnInfo.StateDescriptions["cooked"], "Cooking removes poison.", "migration adds missing English state text");
            Equal(migratedEnInfo.StateChecks["cooked"].MustNotHaveActive[0], "Poison", "migration adds missing English state check");

            JObject edited = JObject.Parse(File.ReadAllText(migratedEnglish.Path_));
            edited["items"]["green crispberry"]["description"] = "HotReloaded custom text";
            File.WriteAllText(migratedEnglish.Path_, edited.ToString());
            File.SetLastWriteTimeUtc(migratedEnglish.Path_, DateTime.UtcNow.AddMinutes(1));
            migratedEnglish.MaybeReload();
            Equal(migratedEnglish.TryGet("Green Crispberry", out ItemInfo reloadedInfo), true, "HotReload retains item lookup");
            Equal(reloadedInfo.Description, "HotReloaded custom text", "HotReload reads changed custom text");

            File.WriteAllText(migratedEnglish.Path_, "{");
            File.SetLastWriteTimeUtc(migratedEnglish.Path_, DateTime.UtcNow.AddMinutes(2));
            migratedEnglish.MaybeReload();
            Equal(migratedEnglish.TryGet("Green Crispberry", out ItemInfo afterInvalidReload), true, "invalid HotReload retains prior item data");
            Equal(afterInvalidReload.Description, "HotReloaded custom text", "invalid HotReload does not discard last good text");

            Zh.Active = true;
            BepInEx.Paths.ConfigPath = Path.Combine(tempRoot, "fresh-zh");
            Directory.CreateDirectory(BepInEx.Paths.ConfigPath);
            var freshChinese = new Descriptions();
            freshChinese.Initialize();
            Equal(freshChinese.TryGet("Green Crispberry", out ItemInfo freshZhInfo), true, "fresh Chinese defaults load");
            Equal(freshZhInfo.Description, "默认中文生食描述。", "fresh Chinese description loads");
            Equal(freshZhInfo.StateDescriptions["cooked"], "烹饪后会去除中毒。", "fresh Chinese state description loads");
        }
        finally
        {
            Zh.Active = false;
            BepInEx.Paths.ConfigPath = "";
            string tempBase = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullTempRoot = Path.GetFullPath(tempRoot).TrimEnd(Path.DirectorySeparatorChar);
            if (!fullTempRoot.StartsWith(tempBase, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to delete a test directory outside the system temp folder.");
            Directory.Delete(fullTempRoot, true);
        }
    }

    private static void Equal(string actual, string expected, string label)
    {
        if (actual != expected) throw new Exception($"{label}: expected '{expected}', got '{actual}'");
        checks++;
    }
    private static void Equal(int actual, int expected, string label)
    {
        if (actual != expected) throw new Exception($"{label}: expected '{expected}', got '{actual}'");
        checks++;
    }
    private static void Equal(bool actual, bool expected, string label)
    {
        if (actual != expected) throw new Exception($"{label}: expected '{expected}', got '{actual}'");
        checks++;
    }
    private static void Has(string actual, string expected, string label)
    {
        if (!actual.Contains(expected)) throw new Exception($"{label}: missing '{expected}' in '{actual}'");
        checks++;
    }
    private static void Lacks(string actual, string excluded, string label)
    {
        if (actual.Contains(excluded)) throw new Exception($"{label}: unexpected '{excluded}' in '{actual}'");
        checks++;
    }
}
