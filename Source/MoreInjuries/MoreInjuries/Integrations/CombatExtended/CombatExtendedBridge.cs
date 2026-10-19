using HarmonyLib;
using MoreInjuries.HealthConditions.VitalFailure;
using MoreInjuries.HealthConditions.HeavyBleeding.Overrides;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace MoreInjuries.Integrations.CombatExtended;

// Optional runtime binding: never load a CE assembly or change a CE wound to obtain its original rate.
internal static partial class CombatExtendedBridge
{
    internal const string CE_HARMONY_OWNER = "CombatExtended.HarmonyCE";
    private static Type? s_compType;
    private static AccessTools.FieldRef<object, float>? s_modifier;
    private static AccessTools.FieldRef<object, bool>? s_stabilized;
    private static bool s_active, s_available, s_verified, s_logged;
    private static readonly List<MethodInfo> s_getters = [];
    private static readonly List<MethodInfo> s_miGetters = [];
    private static CombatExtendedBleedingCapture? s_capture;

    internal static void Initialize()
    {
        s_active = ModsConfig.IsActive(SupportedMods.COMBAT_EXTENDED);
        if (!s_active) { return; }
        Harmony harmony = new("Th3Fr3d.MoreInjuries.CombatExtended");
        try
        {
            s_compType = AccessTools.TypeByName("CombatExtended.HediffComp_Stabilize");
            if (s_compType is null || !typeof(HediffComp).IsAssignableFrom(s_compType))
            { throw new InvalidOperationException("Missing stabilization comp."); }
            s_modifier = AccessTools.FieldRefAccess<float>(s_compType, "bleedModifier");
            s_stabilized = AccessTools.FieldRefAccess<bool>(s_compType, "stabilized");
            MethodInfo stabilize = AccessTools.Method(s_compType, "Stabilize", [typeof(Pawn), typeof(RimWorld.Medicine)])
                ?? throw new InvalidOperationException("Missing Stabilize method.");
            MethodInfo tick = AccessTools.Method(s_compType, "CompPostTickInterval", [typeof(float).MakeByRefType(), typeof(int)])
                ?? throw new InvalidOperationException("Missing stabilization tick method.");
            // Match CE's registration, including modded concrete injury/missing-part subclasses.
            foreach (Type type in typeof(HediffWithComps).AllSubclassesNonAbstract())
            {
                MethodInfo? getter = type.GetProperty("BleedRate")?.GetGetMethod();
                if (getter is null || !getter.IsVirtual || getter.DeclaringType != type) { continue; }
                if (type.FullName is "MoreInjuries.HealthConditions.HeavyBleeding.Overrides.BetterInjury"
                    or "MoreInjuries.HealthConditions.HeavyBleeding.Overrides.BetterMissingPart")
                { s_miGetters.Add(getter); continue; }
                s_getters.Add(getter);
                harmony.Patch(getter, postfix: new HarmonyMethod(typeof(CombatExtendedBridge), nameof(ObserveBleeding))
                { before = [CE_HARMONY_OWNER], priority = Priority.First });
            }
            if (s_getters.Count == 0) { throw new InvalidOperationException("Missing bleeding getters."); }
            harmony.Patch(stabilize, postfix: new HarmonyMethod(typeof(CombatExtendedBridge), nameof(Changed)));
            harmony.Patch(tick, prefix: new HarmonyMethod(typeof(CombatExtendedBridge), nameof(BeforeTick)),
                postfix: new HarmonyMethod(typeof(CombatExtendedBridge), nameof(AfterTick)));
            s_available = true;
        }
        catch (Exception exception)
        {
            harmony.UnpatchAll("Th3Fr3d.MoreInjuries.CombatExtended");
            Unavailable(exception.Message);
        }
    }

    internal static void VerifyRegistration()
    {
        if (!s_available || s_verified) { return; }
        Type? patchType = AccessTools.TypeByName("CombatExtended.HarmonyCE.Harmony_HediffWithComps_BleedRate_Patch");
        MethodInfo? postfix = patchType is null ? null : AccessTools.Method(patchType, "Postfix", [typeof(Hediff), typeof(float).MakeByRefType()]);
        if (postfix is null || !CombatExtendedBleedingHooks.Normalize(new Harmony("Th3Fr3d.MoreInjuries.CombatExtended"),
            postfix, s_getters, s_miGetters)) { Unavailable("Unrecognized CE bleeding patch registration."); }
        s_verified = true;
    }

    private static void Unavailable(string reason)
    {
        s_available = false;
        if (s_logged) { return; }
        s_logged = true;
        Log.Warning("More Injuries: CE stabilization forecasting is unavailable; using current bleeding only. " + reason);
    }

    internal static void Capture(Pawn pawn, ref PhysiologySnapshot source, ref CombatExtendedBleedingCapture? capture)
    {
        VerifyRegistration();
        if (s_active && !s_available) { source.UnmodeledRisk = true; }
        capture ??= new();
        capture.Count = 0;
        capture.WoundCount = 0;
        capture.Patient = pawn;
        capture.NextTick = source.NextBloodTick;
        if (!pawn.health.CanBleed) { return; }
        if (pawn.HealthScale <= 0f) { source.UnmodeledRisk = true; return; }
        CombatExtendedBleedingCapture? previous = s_capture;
        s_capture = capture;
        float woundTotal = 0f;
        try
        {
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                capture.Wound = hediff;
                capture.NativeReads = 0;
                float current = hediff.BleedRate;
                woundTotal += current;
                float constant = 1f, timedBase = 1f; int remaining = 0, duration = 0;
                if (hediff is IStatefulInjury { State: IForecastHemostasis state })
                { state.CaptureHemostasis(out constant, out timedBase, out remaining, out duration); }
                HediffComp? comp = FindStabilization(hediff);
                float modifier = comp is null ? 1f : s_modifier!(comp);
                bool decay = comp is not null && s_stabilized!(comp) && modifier < 1f;
                bool missing = comp is not null && hediff is Hediff_MissingPart;
                bool inside = comp is not null && hediff.Part?.depth == BodyPartDepth.Inside;
                bool temporary = !hediff.IsTended() && !hediff.IsPermanent() && (decay || duration > 0);
                source.TemporaryBleedingRisk |= temporary;
                source.CeStabilization |= decay;
                // A zero result cannot reveal a baseline by division. Missing observation must
                // never silently become a zero-rate profile and an assurance of future safety.
                bool missingBaseline = comp is not null && decay && capture.NativeReads != 1 && current <= 0f;
                bool unsupportedSuppression = s_active && !s_available && current <= 0f
                    && !hediff.IsTended() && !hediff.IsPermanent()
                    && (hediff.def.injuryProps?.bleedRate > 0f || hediff is Hediff_MissingPart);
                if (missingBaseline || unsupportedSuppression)
                { source.BleedingUncertain = source.TemporaryBleedingRisk = source.UnmodeledRisk = true; }
                PrognosisBleeding unit = new(1f, modifier, missing, inside, capture.NextTick, decay, constant, timedBase, remaining, duration);
                float divisor = unit.At(0);
                float baseline = comp is not null && capture.NativeReads == 1 ? capture.NativeRate : divisor > 0f ? current / divisor : 0f;
                PrognosisBleeding profile = new(baseline, modifier, missing, inside, capture.NextTick, decay, constant, timedBase, remaining, duration);
                if (capture.NativeReads > 1 || missingBaseline || Math.Abs(profile.At(0) - current) > .00001f)
                { source.UnmodeledRisk = true; profile = new(current, 1f, false, false, capture.NextTick, false); decay = false; }
                if (capture.WoundCount == capture.Wounds.Length)
                { Array.Resize(ref capture.Wounds, capture.WoundCount * 2); Array.Resize(ref capture.WoundEntries, capture.WoundCount * 2); }
                capture.Wounds[capture.WoundCount] = hediff;
                capture.WoundEntries[capture.WoundCount++] = profile;
                if (baseline <= 0f || !decay && duration <= 0) { continue; }
                if (capture.Count == capture.Entries.Length) { Array.Resize(ref capture.Entries, capture.Count * 2); }
                capture.Entries[capture.Count++] = profile;
            }
        }
        finally { s_capture = previous; }
        // Preserve native aggregate factors and suppression when rates are nonzero. At complete CE
        // suppression, native pawn-total units are wound rates / HealthScale; CanBleed is guarded above.
        float scale = woundTotal > 0f ? source.BleedRate / woundTotal : 1f / pawn.HealthScale;
        for (int i = 0; i < capture.Count; i++) { capture.Entries[i] = capture.Entries[i].Scaled(scale); }
        source.Bleeding = capture.Entries;
        source.BleedingCount = capture.Count;
        source.Relevant |= capture.Count > 0 || source.TemporaryBleedingRisk;
    }

    // An ordinary postfix ordered before CE observes the original value without suppressing any patch.
    internal static void ObserveBleeding(Hediff __instance, float __result)
    {
        if (s_capture is not { } capture || __instance != capture.Wound || __instance.pawn != capture.Patient) { return; }
        capture.NativeReads++;
        capture.NativeRate = __result;
    }

    private static HediffComp? FindStabilization(Hediff hediff)
    {
        if (!s_available || hediff is not HediffWithComps { comps: { } comps }) { return null; }
        foreach (HediffComp comp in comps)
        {
            if (s_compType!.IsInstanceOfType(comp)) { return comp; }
        }
        return null;
    }

    internal static void Changed(HediffComp __instance) => VitalFailureComp.For(__instance.parent.pawn)?.InvalidateForecast();
    internal static void BeforeTick(HediffComp __instance, out float __state) => __state = s_modifier!(__instance);
    internal static void AfterTick(HediffComp __instance, float __state)
    {
        if (s_modifier!(__instance) != __state) { Changed(__instance); }
    }
}

internal sealed class CombatExtendedBleedingCapture
{
    internal Pawn Patient = null!;
    internal Hediff? Wound;
    internal int NextTick, Count;
    internal int NativeReads, WoundCount;
    internal float NativeRate;
    internal PrognosisBleeding[] Entries = new PrognosisBleeding[4];
    internal PrognosisBleeding[] WoundEntries = new PrognosisBleeding[4];
    internal Hediff?[] Wounds = new Hediff?[4];

    internal PrognosisBleeding For(Hediff wound)
    {
        for (int i = 0; i < WoundCount; i++) { if (Wounds[i] == wound) { return WoundEntries[i]; } }
        return new(wound.BleedRate, 1f, false, false, NextTick, false);
    }
}
