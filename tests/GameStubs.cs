// Minimal managed model for testing the production formatting code without redistributing
// game assemblies or loading Unity native code. Cooking mutations are supplied by the tests.
using System;
using System.Collections.Generic;
using System.Linq;

namespace BepInEx
{
    public static class Paths { public static string ConfigPath = ""; }
}

namespace UnityEngine
{
    public class GameObject
    {
        public bool activeSelf = true;
        public readonly Transform transform;
        public GameObject(Transform parent = null) { transform = new Transform(this, parent); }
    }

    public class Transform
    {
        public readonly GameObject gameObject;
        public readonly Transform parent;
        public Transform(GameObject gameObject, Transform parent) { this.gameObject = gameObject; this.parent = parent; }
    }

    public class MonoBehaviour
    {
        public bool enabled = true;
        public Transform transform;
    }

    public struct Color
    {
        public static Color white => default;
    }

    public static class ColorUtility
    {
        public static bool TryParseHtmlString(string value, out Color colour) { colour = default; return true; }
        public static string ToHtmlStringRGB(Color colour) => "FFFFFF";
    }

    public static class Mathf
    {
        public static int RoundToInt(float value) => (int)Math.Round(value);
        public static int Abs(int value) => Math.Abs(value);
    }
}

public class Item : UnityEngine.MonoBehaviour
{
    public ItemCooking cooking;
    private Optionable<bool> overrideUsability = new Optionable<bool> { IsNone = true };
    public readonly List<UnityEngine.MonoBehaviour> components = new List<UnityEngine.MonoBehaviour>();
    public Item() { transform = new UnityEngine.GameObject().transform; }
    public T Add<T>(T component, UnityEngine.Transform branch = null) where T : UnityEngine.MonoBehaviour
    {
        component.transform = branch ?? transform;
        components.Add(component);
        return component;
    }
    public T[] GetComponentsInChildren<T>(bool includeInactive) => components.OfType<T>().ToArray();
    public void SetUsabilityOverride(bool canUse) { overrideUsability = new Optionable<bool> { IsNone = false, Value = canUse }; }
}

public class Optionable<T> { public bool IsNone { get; set; } public T Value { get; set; } }

public class ItemCooking { public int timesCookedLocal; public bool wreckWhenCooked; }
public class ItemActionBase : UnityEngine.MonoBehaviour { }
public class ItemAction : ItemActionBase
{
    public bool OnPressed, OnHeld, OnReleased, OnCastFinished, OnCancelled;
    public bool OnSecondaryCastFinished, OnSecondaryPressed, OnSecondaryHeld, OnSecondaryCancelled, OnConsumed;
}
public class CustomUnknownAction : ItemActionBase { }
public class Action_RestoreHunger : ItemAction { public float restorationAmount; }
public class Action_InflictPoison : ItemAction { public float poisonPerSecond, inflictionTime, delay; }
public class Action_ModifyStatus : ItemAction { public CharacterAfflictions.STATUSTYPE statusType; public float changeAmount; public bool ifSkeleton; }
public class Action_GiveExtraStamina : ItemAction { public float amount; }
public class Action_AddOrRemoveThorns : ItemAction { public int thornCount; }
public class Action_ClearAllStatus : ItemAction { }
public class Action_Consume : ItemAction { }
public class Action_ReduceUses : ItemAction { }
public class Action_PlayAnimation : ItemAction { }
public class Action_PlayItemAnimation : ItemAction { }
public class Action_IncrementSteamStat : ItemAction { }
public class Action_TryHotDogAchievement : ItemAction { }
public class Action_ApplyAffliction : ItemAction
{
    public Peak.Afflictions.Affliction affliction;
    public Peak.Afflictions.Affliction[] extraAfflictions;
}
public class Action_ApplyMassAffliction : Action_ApplyAffliction { public float radius; public bool ignoreCaster; }
public class Action_RandomMushroomEffect : ItemAction { }
public class Action_ApplyInfiniteStamina : ItemAction { public float buffTime; public float drowsyAmount = .25f; }
public class PetrifySource : UnityEngine.MonoBehaviour { public int petrifyPerJump; }
public class CharacterAfflictions
{
    public enum STATUSTYPE { Injury, Hunger, Cold, Poison, Crab, Curse, Drowsy, Weight, Hot, Thorns, Spores, Web, Arrow, Petrify, FlyTrap }
}
namespace Peak.Afflictions
{
    public class Affliction { }
    public class Affliction_AdjustStatus : Affliction
    {
        public CharacterAfflictions.STATUSTYPE statusType;
        public float statusAmount;
    }
    public class Affliction_PoisonOverTime : Affliction
    {
        public float totalTime;
        public float delayBeforeEffect;
        public float statusPerSecond;
    }
    public class Affliction_AdjustColdOverTime : Affliction
    {
        public float statusPerSecond;
        public float totalTime;
    }
    public class Affliction_AdjustDrowsyOverTime : Affliction
    {
        public float statusPerSecond;
        public float totalTime;
    }
    public class Affliction_AddBonusStamina : Affliction
    {
        public float staminaAmount;
        public float totalTime;
    }
    public class Affliction_InfiniteStamina : Affliction { public float totalTime; public float climbDelay; public Affliction drowsyAffliction; }
    public class Affliction_Invincibility : Affliction { public float totalTime; }
    public class Affliction_Sunscreen : Affliction { public float totalTime; }
    public class Affliction_Numb : Affliction { public float totalTime; }
    public class Affliction_Blind : Affliction { public float totalTime; }
    public class Affliction_Glowing : Affliction { public float totalTime; }
    public class Affliction_NoHunger : Affliction { public float totalTime; }
    public class Affliction_BingBongShield : Affliction { public float totalTime; }
    public class Affliction_HealAll : Affliction { public float maxHealing; }
    public class Affliction_ClearAllStatus : Affliction { public bool excludeCurse; public bool includePetrify; }
    public class Affliction_Exhaustion : Affliction { public float totalTime; public float drainAmount; }
    public class Affliction_Chaos : Affliction
    {
        public float statusAmountAverage, statusAmountStandardDeviation, averageBonusStamina, standardDeviationBonusStamina;
    }
    public class Affliction_ZombieBite : Affliction { public float totalTime, delayBeforeEffect, statusPerSecond; }
    public class Affliction_RadiateInfiniteStam : Affliction { public float totalTime, radius; }
    public class UnknownAffliction : Affliction { }
}
namespace Peak
{
    public class Action_HealingGem : ItemAction { public float minPetrify, maxPetrify; }
    public class Action_SuperJumpAmulet : Action_ApplyAffliction { public float petrifyPerUse; }
}
namespace PeakItemTooltip
{
    public static class Zh
    {
        public static bool Active;
        public static bool IsActive() => Active;
    }

    public static class Plugin
    {
        public static readonly TestLogger Log = new TestLogger();
    }

    public sealed class TestLogger
    {
        public void LogInfo(object message) { }
        public void LogWarning(object message) { }
    }
}
