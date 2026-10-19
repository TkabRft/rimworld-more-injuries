using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

[StaticConstructorOnStartup]
internal static class NpcMedicalCompatibility
{
    private static readonly Harmony s_harmony = new("Th3Fr3d.MoreInjuries.NpcCombatMedicine");
    private static Type? s_inventory;
    private static MethodInfo? s_fit, s_update;
    private static PropertyInfo? s_cePawn;
    private static bool s_ceActive;
    private static readonly HashSet<string> s_reported = [];

    static NpcMedicalCompatibility()
    {
        if (ModsConfig.IsActive("CETeam.CombatExtended"))
        {
            s_ceActive = true;
            s_inventory = OwnedType("CombatExtended.CompInventory", "CombatExtended");
            s_fit = s_inventory?.GetMethod("CanFitInInventory", [typeof(Thing), typeof(int).MakeByRefType(), typeof(bool), typeof(bool)]);
            s_update = s_inventory is null ? null : AccessTools.Method(s_inventory, "UpdateInventory", Type.EmptyTypes);
            if (s_fit?.ReturnType != typeof(bool) || s_update?.ReturnType != typeof(void)) { s_fit = null; s_update = null; }
            if (s_fit is null || s_update is null) { Log.Warning("More Injuries: unrecognized CE inventory API; NPC medical provisioning is disabled for CE inventories."); }
            s_cePawn = OwnedType("CombatExtended.AI.CompTend", "CombatExtended")?.GetProperty("SelPawn");
            if (s_cePawn?.PropertyType == typeof(Pawn)) { Bind("CombatExtended", "CombatExtended.AI.CompTend", "TryGiveTacticalJob", nameof(CeTend), typeof(Job), false); }
            else { Log.Warning("More Injuries: unrecognized CE tactical pawn API; retaining CE's medical decisions."); }
        }
        if (ModsConfig.IsActive("Mlie.EnemySelfPreservation"))
        { Bind("EnemySelfPreservation", "MUR_ESP.Pawn_PostApplyDamage", "Postfix", nameof(Preservation), typeof(void), true, typeof(Pawn).MakeByRefType()); }
        if (ModsConfig.IsActive("NinaGoblin.NoOneLeftBehind"))
        { Bind("CM_No_Pawn_Left_Behind", "CM_No_Pawn_Left_Behind.JobGiver_Rescue", "TryGiveJob", nameof(Rescue), typeof(Job), false, typeof(Pawn)); }
        if (ModsConfig.IsActive("Memegoddess.SmartMedicine"))
        {
            Bind("SmartMedicine", "SmartMedicine.FindBestMedicine", "Prefix", nameof(SmartFind), typeof(bool), true,
                typeof(Pawn), typeof(Pawn), typeof(Thing).MakeByRefType(), typeof(bool));
            Bind("SmartMedicine", "SmartMedicine.JobOnThing_Patch", "Prefix", nameof(SmartJob), typeof(bool), true,
                typeof(Pawn), typeof(Thing), typeof(Job).MakeByRefType());
            Bind("SmartMedicine", "SmartMedicine.TendPatient_Notify_Starting_Patch", "Prefix", nameof(SmartStarting), typeof(void), true, typeof(JobDriver_TendPatient));
        }
    }

    private static Type? OwnedType(string name, string assembly)
    {
        Type? type = AccessTools.TypeByName(name);
        return type?.Assembly.GetName().Name == assembly ? type : null;
    }

    private static void Bind(string assembly, string name, string method, string prefix, Type result, bool isStatic, params Type[] parameters)
    {
        try
        {
            Type type = OwnedType(name, assembly) ?? throw new MissingMemberException(name);
            MethodInfo target = type.GetMethod(method, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static
                | BindingFlags.Public | BindingFlags.NonPublic, null, parameters, null) ?? throw new MissingMethodException(name, method);
            if (target.ReturnType != result || target.IsStatic != isStatic) { throw new MissingMethodException(name, method); }
            s_harmony.Patch(target, prefix: new HarmonyMethod(typeof(NpcMedicalCompatibility), prefix));
        }
        catch (Exception exception)
        {
            if (s_reported.Add(assembly))
            { Log.Warning("More Injuries: optional NPC medical adapter unavailable for " + name + ": " + exception.Message); }
        }
    }

    private static object? Inventory(Pawn pawn) => s_inventory is null ? null : pawn.AllComps.FirstOrDefault(comp => s_inventory.IsInstanceOfType(comp));
    internal static int Fit(Pawn pawn, Thing thing)
    {
        if (s_ceActive && (s_inventory is null || s_fit is null || s_update is null)) { return 0; }
        object? inventory = Inventory(pawn);
        if (inventory is not null)
        {
            if (s_fit is null) { return 0; }
            try
            {
                object[] args = [thing, 0, false, false];
                s_fit.Invoke(inventory, args);
                return (int)args[1];
            }
            catch (Exception exception) { InventoryUnavailable(exception); return 0; }
        }
        float mass = thing.GetStatValue(StatDefOf.Mass);
        return mass <= 0f ? thing.stackCount : Math.Max(0,
            (int)((MassUtility.Capacity(pawn) - MassUtility.GearAndInventoryMass(pawn)) / mass));
    }
    internal static void InventoryChanged(Pawn pawn)
    {
        if (Inventory(pawn) is not object inventory || s_update is null) { return; }
        try { s_update.Invoke(inventory, null); }
        catch (Exception exception) { InventoryUnavailable(exception); }
    }
    private static void InventoryUnavailable(Exception exception)
    {
        if (s_fit is null && s_update is null) { return; }
        s_fit = null; s_update = null;
        Log.Warning("More Injuries: CE NPC medical provisioning stopped after an inventory API failure. " + exception.Message);
    }

    private static bool CeTend(object __instance, ref Job? __result)
    {
        Pawn? pawn = s_cePawn?.GetValue(__instance) as Pawn;
        if (pawn is null || !NpcMedicalAi.Managed(pawn)) { return true; }
        if (NpcMedicalAi.Owns(pawn.CurJob)) { __result = null; return false; }
        Job? job = NpcMedicalAi.Decide(pawn);
        if (job is null) { return true; }
        __result = job;
        return false;
    }
    private static bool Preservation([HarmonyArgument(0)] ref Pawn pawn)
        => !NpcMedicalAi.OwnsSurvival(pawn);
    private static bool Rescue([HarmonyArgument(0)] Pawn pawn, ref Job? __result)
    {
        if (!NpcMedicalAi.OwnsSurvival(pawn)) { return true; }
        __result = null;
        return false;
    }
    private static bool SmartFind([HarmonyArgument(0)] Pawn doctor, [HarmonyArgument(1)] Pawn patient,
        [HarmonyArgument(2)] ref Thing? medicine, ref bool __result)
    {
        if (!NpcMedicalAi.Managed(doctor)) { return true; }
        medicine = NpcMedicalAi.FindMedicine(doctor, patient);
        __result = false; // This is the other prefix's return: skip vanilla's unbounded search too.
        return false;
    }
    private static bool SmartJob([HarmonyArgument(0)] Pawn doctor, [HarmonyArgument(1)] Thing target,
        [HarmonyArgument(2)] ref Job? job, ref bool __result)
    {
        if (!NpcMedicalAi.Managed(doctor) || target is not Pawn patient) { return true; }
        Thing? medicine = NpcMedicalAi.FindMedicine(doctor, patient);
        job = JobMaker.MakeJob(KnownJobDefOf.MI_NpcTend, patient, medicine);
        job.count = medicine is null ? 0 : 1;
        job.endAfterTendedOnce = true;
        __result = false;
        return false;
    }
    private static bool SmartStarting([HarmonyArgument(0)] JobDriver_TendPatient driver)
        => !NpcMedicalAi.Managed(driver.pawn) || driver.job.def != KnownJobDefOf.MI_NpcTend;
}
