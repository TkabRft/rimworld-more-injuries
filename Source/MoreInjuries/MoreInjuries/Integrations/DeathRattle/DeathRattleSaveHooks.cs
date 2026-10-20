using HarmonyLib;
using MoreInjuries.Versioning;
using RimWorld;
using System;
using System.IO;
using System.Xml;
using Verse;

namespace MoreInjuries.Integrations.DeathRattle;

[HarmonyPatch(typeof(ScribeSaver), nameof(ScribeSaver.InitSaving))]
internal static class Patch_DeathRattle_SavePath
{
    internal static string? PendingPath;
    internal static void Postfix(string filePath, string documentElementName)
        => PendingPath = documentElementName == "savegame" ? filePath : null;
}

[HarmonyPatch(typeof(ScribeSaver), nameof(ScribeSaver.FinalizeSaving))]
internal static class Patch_DeathRattle_FinalizeSave
{
    internal static void Prefix(out string? __state)
    {
        __state = Patch_DeathRattle_SavePath.PendingPath;
        Patch_DeathRattle_SavePath.PendingPath = null;
    }

    internal static void Postfix(string? __state)
    {
        GameComponent_DeathRattleTransition? transition = Current.Game?.GetComponent<GameComponent_DeathRattleTransition>();
        if (__state is null || !DeathRattleBridge.Active || transition?.Ready != true) { return; }
        string? temporary = null;
        try
        {
            XmlDocument document = new() { PreserveWhitespace = true, XmlResolver = null };
            using (XmlReader reader = XmlReader.Create(__state, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            { document.Load(reader); }
            TransitionSaveValidator.RemapReferences(document, transition.ReferenceReplacements);
            string? residual = TransitionSaveValidator.FindResidual(document, DeathRattleBridge.SourceIdentifiers);
            if (residual is not null) { throw new InvalidOperationException(residual); }
            TransitionSaveValidator.RemoveDependency(document);
            temporary = __state + ".moreinjuries-" + Guid.NewGuid().ToString("N") + ".tmp";
            document.Save(temporary);
            // Same-directory atomic replacement: a failure retains the original complete save.
            File.Replace(temporary, __state, null);
            temporary = null;
        }
        catch (Exception exception)
        {
            Logger.Error("More Injuries transition save validation: " + exception);
            Find.LetterStack.ReceiveLetter("MI_DR_TransitionTitle".Translate(), "MI_DR_SaveBlocked".Translate(exception.Message), LetterDefOf.NegativeEvent);
        }
        finally
        {
            if (temporary is not null && File.Exists(temporary)) { File.Delete(temporary); }
        }
    }
}
