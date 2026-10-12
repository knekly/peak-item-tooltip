# PEAK 2.6.b effect-source audit

This is a code-level audit of the local PEAK 2.6.b `Assembly-CSharp.dll`. It records what the tooltip can read safely today; it does not claim that every item's serialized prefab has been mapped or that in-game behavior has been verified.

## Item actions

The local assembly inventory contained 56 `Action_*` classes. Their implementations were decompiled and grouped by how they affect tooltip claims.

| Source family | Current handling | Coverage status |
|---|---|---|
| `Action_RestoreHunger` | Reads the live hunger restoration value and renders a signed row. | Numeric row supported |
| `Action_InflictPoison` | Reads poison rate and duration, renders the aggregate amount, and labels delay/duration. | Numeric row and timing supported |
| `Action_ModifyStatus` | Reads signed status changes. Positive recognized statuses participate in safety checks. Non-default `ItemAction` triggers and the `ifSkeleton` condition are included in the row label. | Numeric row supported; `ifSkeleton` still makes a categorical safety claim incomplete |
| `Action_GiveExtraStamina` | Reads live extra stamina. | Numeric row supported |
| `Action_ApplyInfiniteStamina` | Reads live `buffTime` as a duration note. PEAK 2.6.b `RunAction` constructs an Infinite Stamina payload without an end affliction; its serialized `drowsyAmount` is unused. | Duration supported; does not invent a Drowsy penalty from an unused field |
| `Action_AddOrRemoveThorns` | Reads the direct thorn count, renders add/remove values, and detects positive Thorns for safety checks. | Numeric row supported |
| `Action_ClearAllStatus` | Decompilation confirms it only subtracts statuses, with exclusions. | Safe for absence checks; not represented as numeric rows |
| `Action_Consume`, `Action_ReduceUses`, animation actions, `Action_IncrementSteamStat`, `Action_TryHotDogAchievement` | Consumption, visual, or achievement plumbing. | Accounted for when checking adverse-effect claims |
| `Action_ApplyAffliction` with `Affliction_AdjustStatus` | Reads the signed live status amount from the primary and extra afflictions. | Numeric row supported; participates in safety checks |
| `Action_ApplyAffliction` with `Affliction_PoisonOverTime` | Reads poison rate, total time, and delay from primary and extra afflictions; shows the aggregate amount plus timing. | Numeric row supported; participates in safety checks |
| `Action_ApplyAffliction` with `Affliction_AdjustColdOverTime` / `Affliction_AdjustDrowsyOverTime` | Reads the signed per-second status rate and duration from primary and extra afflictions. | Numeric row supported; aggregate amount and duration shown; participates in safety checks |
| `Action_ApplyAffliction` with `Affliction_AddBonusStamina` | Reads extra stamina and duration from primary and extra afflictions. | Numeric row supported; participates in safety checks |
| `Action_ApplyAffliction` with `Affliction_Invincibility`, `Affliction_Sunscreen`, `Affliction_Numb`, `Affliction_Blind`, `Affliction_Glowing` | Reads the current payload's `totalTime` and renders a named duration note, without treating duration as signed status points. | Duration notes supported; known pure payloads are accounted for |
| `Action_ApplyAffliction` with `Affliction_InfiniteStamina` | Reads base duration, conditional climbing grace, and a supported end-time Drowsy payload. | Primary duration note and signed end penalty supported; unrecognized end payload remains incomplete while retaining the known primary note |
| `Peak.Action_SuperJumpAmulet` (derived from `Action_ApplyAffliction`) | Reads supported primary/extra afflictions and its direct `petrifyPerUse` value. | Numeric rows supported; Petrify participates in safety checks |
| Other `Action_ApplyAffliction` payloads | Other affliction types can contain distinct buffs, conditions, or status behavior. | Unsupported; any active instance suppresses a checked safety claim |
| `Action_ApplyMassAffliction` | Renders the same nineteen supported primary/extra payload families, with the live item-centered radius and holder exclusion. Missing, negative or non-finite radius information prevents unqualified rows. A null primary prevents extras, matching the game. | Conditional values and duration notes supported; actual target eligibility is not evaluated, so categorical self-safety checks remain incomplete |
| `Action_RandomMushroomEffect` | Reads the current mushroom-manager roll and can produce delayed positive or negative effects, including Poison/Spores changes. | Random outcome is not available from the item alone; treated as incomplete |
| `Action_ConsumeAndSpawn` | Consumes the source and creates another item after a delay. | Replacement chain is not followed; treated as incomplete |
| Other active `ItemActionBase` subclasses and `Action_*` components | Not assumed harmless based only on their names. | Treated as incomplete unless explicitly accounted for |

`ItemAction.Subscribe()` wires press, hold, cast-finished, cancel, secondary-action, and consumed events. The serialized `OnReleased` field is present in this build but is not subscribed by that base implementation, so the tooltip does not label it as an active trigger. Ordinary consume-only actions omit a redundant trigger suffix; non-default triggers are labeled on their numeric rows.

When a legacy state description has a `mustNotHaveActive` check, incomplete effect/condition coverage suppresses that state sentence and shows an uncertainty note. A recognized active conflicting status suppresses that sentence and reports the conflict. Complete shared prose remains available with the corresponding notice; it describes conditional behavior rather than asserting the current numeric outcome. Disabled actions and inactive child branches do not count as active sources.

The mass-action RPC checks distance from the item, not from the user. Its `ignoreCaster` flag compares against `item.holderCharacter`; rows therefore say "holder excluded" rather than promising that the original thrower is excluded. Scope is labeled **on application**, including delayed end effects: recipients are chosen initially, not reselected when Infinite Stamina ends. Area rows are not added to unconditional self-status detection. The formatter does not call RPCs or enumerate current characters to predict hits. Regression coverage includes radius/exclusion mutations, extra payload scope, snapshot stability, invalid radius, null primary, disabled source, Chinese qualifiers and retained safety uncertainty. Notices now distinguish incomplete sources or target conditions from a proven absence of effects.

This is a **safety-claim guard**, not full effect coverage: unsupported effects may still be absent from the numeric rows. Remaining non-status buffs, conditional afflictions, unsupported area payloads, and replacement chains still need source-specific rendering and tests.

## Affliction inventory

The PEAK 2.6.b assembly contains 26 `Peak.Afflictions` classes. Nineteen payload families are represented by signed values, duration notes, or conditional/random behavior notes. The remaining seven are inventoried below and retain incomplete coverage. Supported payloads with invalid/unreadable data, target-dependent outcomes or unsupported nested effects also remain incomplete.

| Affliction payloads | Decompiled behavior | Current reader decision |
|---|---|---|
| `Affliction_AdjustStatus`, `Affliction_PoisonOverTime`, `Affliction_AdjustColdOverTime`, `Affliction_AdjustDrowsyOverTime`, `Affliction_AddBonusStamina` | Signed status adjustment; timed Poison; timed Cold/Drowsy; or temporary bonus stamina. | Parsed when fields are readable; numeric amount and applicable timing are shown. |
| `Affliction_AdjustStatusOverTime` | Carries a status rate and duration but has no `UpdateEffect` override in this game build; the base update is empty. | Not rendered or whitelisted; remains conservative until its source is intentionally supported. |
| `Affliction_Chaos` | Clears curable statuses including Curse but excluding Petrify, randomly redistributes adverse statuses, and replaces Extra Stamina. | Qualitative random-outcome note supported after validating distribution fields; means are never reported as guaranteed doses. Safety coverage remains incomplete. |
| `Affliction_ZombieBite` | Adds Spores after a delay but terminates whenever current Spores is below 0.025. | Reads live rate, delay and duration; shows an upper bound with the 2.5-point termination threshold. Target-state safety coverage remains incomplete. |
| `Affliction_PreventPoisonHealing` | Temporarily prevents Poison healing. | Unsupported: conditional gameplay rule, not a numeric status delta. |
| `Affliction_Exhaustion` | Calls UseStamina(drainAmount / totalTime * deltaTime); ascent scaling applies and Infinite Stamina bypasses consumption. | Base total drain and duration note supported with these conditions. Invalid/zero durations are omitted; safety coverage remains incomplete. |
| `Affliction_FasterBoi` | Changes movement/climbing and Drowsy, including an end-time Drowsy effect. | Unsupported: movement modifiers and delayed side effects. |
| `Affliction_InfiniteStamina` | Temporarily grants infinite stamina and may apply a serialized Drowsy affliction when it ends. `Affliction.Tick` counts down bonus time before base time; climbing cancels the grace. | Base duration and conditional grace are shown. A valid `Affliction_AdjustDrowsyOverTime` end payload is shown as a signed row explicitly after the buff ends. Other end payloads are not assumed compatible with the game's hardcoded deserializer; coverage stays incomplete without recursion. |
| `Affliction_RadiateInfiniteStam` | Every 0.5s applies a 1s Infinite Stamina buff to a local player within radius of the recipient, including the recipient. | Live radius/duration and refresh behavior note supported; distinguishes recipient-centered aura from item-centered mass action. Safety coverage remains incomplete. |
| `Affliction_MassSuperJump` | Applies movement effects based on radius and target state. | Unsupported: area/teammate movement and target conditions. |
| `Affliction_HealAll` | Spends one maxHealing budget in order: Injury, Spores, Poison, Cold, Hot, Drowsy. | Live shared upper bound and order supported; never reports the budget separately for each status. Valid payload is reduction-only for safety checks. |
| `Affliction_ClearAllStatus` | Clears curable statuses, always excluding Crab, Weight, Thorns and Arrow, optionally excluding Curse/Petrify. | Reads live excludeCurse/includePetrify flags and lists exclusions. Valid payload is reduction-only for safety checks. |
| `Affliction_Invincibility`, `Affliction_Blind`, `Affliction_Numb`, `Affliction_Glowing`, `Affliction_Sunscreen` | Change invincibility, vision, Stamina-bar visibility, glow or sun protection for a duration. | Named duration notes are supported for finite, nonnegative `totalTime`; they do not promise the target's stacked remaining duration. |
| `Affliction_BingBongShield` | CharacterData.RecalculateInvincibility enables invincibility. | Named shield duration note supported. |
| `Affliction_NoHunger` | canGetHungry rejects positive Hunger additions while the buff is active; existing Hunger is not cleared. | Named prevention/duration note supported, explicitly distinguishes prevention from restoration. |
| `Affliction_LowGravity`, `Affliction_ClimbingChalk`, `Affliction_DoubleJumpAmulet` | Change gravity, climbing cost or jump count. | Unsupported: source-specific behavior is not yet represented. |

This inventory records decompiled behavior, not which items instantiate each payload. The installed resource scan found no serialized food `ItemCooking`/`CookingBehavior` instances, so mapping these classes to specific food variants and verifying their final cooking state remains open.

## Cooking behavior

`ItemCooking.UpdateCookedBehavior` skips vanilla default stat changes when `ignoreDefaultCookBehavior` is true, but still runs additional cooking behaviors. The default `ChangeStatsCooked` behavior changes hunger and extra stamina and, at cook count 4 or above, adds Poison unless `ignoreDefaultPoisonBehavior` is true. Item-specific additional behaviors can disable/enable scripts, change an affliction's duration, adjust a holder's status immediately, run actions, add a Poison action, replace the item, wreck it, or explode it.

Notable verified details:

- `CookingBehavior_DisableScripts` disables selected components; the modifier reader ignores disabled components.
- `CookingBehavior_AddPoisonOnUse` adds an `Action_ModifyStatus` Poison component with an `OnConsumed` or `OnCastFinished` trigger; the existing status reader then sees its live value.
- `CookingBehavior_ChangeAfflictionTime` mutates an `Action_ApplyAffliction` payload's `totalTime`. The modifier reader reflects the updated duration for supported timed Poison, Cold, Drowsy, bonus stamina, Invincibility, Sunscreen, Numb, Blind, Glowing and Infinite Stamina payloads; other affliction types remain incomplete.
- `CookingBehavior_ReplaceItem` destroys the source and spawns a replacement, optionally cooking it once.
- `CookingBehavior_Wreck` calls `ItemCooking.Wreck()`, which destroys item actions and sets the private `overrideUsability` option to false without setting `wreckWhenCooked`. State selection now reads that explicit unusable override as well.
- Additional cooking behaviors run even when `ignoreDefaultCookBehavior` skips vanilla stat changes.

The tooltip does not execute cooking or action methods while reading an item. Numeric labels include non-default `ItemAction` triggers (such as press/hold/cast) and the skeleton-only condition where present; ordinary `OnConsumed` rows stay concise. The in-game matrix is still required to confirm each affected prefab's configured thresholds and final behavior.

## Verification and remaining work

- Regression tests cover known numeric sources, direct Thorns, signed `Affliction_AdjustStatus`, primary/extra `Affliction_PoisonOverTime` payloads with timing, timed Cold/Drowsy and bonus stamina payloads, the derived Super Jump Amulet action and its Petrify value, trigger/condition labels for direct and affliction-backed rows, unsupported afflictions, mass-target conditions, disabled actions, unknown action subclasses, random mushroom effects, conflicting live statuses, and the `CookingBehavior_Wreck` usability override.
- Configuration migration is factored into the production `DescriptionDefaultsMerger` and tested with old/custom JSON fixtures plus file-backed initialization, English/Chinese embedded defaults, HotReload, and invalid-JSON last-good-data retention.
- Current automated suite: 211 regression checks, including resource budgets, clearing flags, conditional drain, target-centered aura, conditional Spores and random-outcome notes. PEAK 2.6.b Release build: 0 warnings and 0 errors.
- Remaining source work: add explicit support for useful non-status/conditional afflictions, mass-action targets/conditions, and random mushroom outcomes; map the relevant serialized item prefabs where available; then validate the matrix in PEAK itself.
- No item is declared fully effect-covered solely because this guard passed. Only the specific recognized action families above count as supported.

## User smoke test — 2026-10-12

The user authorized installation of DLL SHA-256 `9CE014D9825ED66AA29FFEDF2756F40F4D357E34E746B8CD6B1FAD893D6F440A` into Gale Basic and subsequently reported that the cooking display appears correct (initial manual smoke test passed). The original DLL was backed up. No per-item screenshots, exact tested states, or full item matrix were supplied; this is not evidence of all-item coverage. At the time of the implementation commit, the new resource/conditional readers had automated/source checks only. They were subsequently installed into Gale Basic with backup and hash verification: `BDBAA73A8E1EC3D54D62DF60A74DAD4B4563FC46B10DF7E2C37737538DC7133A`. This newer build has not been game-tested or confirmed loaded in a new BepInEx session.

A full local-assembly source scan found no consumer of `Affliction_ClimbingChalk.climbStaminaMultiplier` outside its own stacking/serialization methods, and no gameplay query of `AfflictionType.PreventPoisonHealing` outside construction/type reporting. Those class names alone do not establish the advertised behavior in this build; they remain unsupported pending actual runtime evidence or another assembly consumer. Low Gravity is consumed by CharacterBalloons together with live balloon coefficients, so its integer level must not be advertised as a fixed gravity percentage. Proprietary decompiled source remains in local scratch only.
