using HarmonyLib;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace MoreInjuries;

public class MoreInjuriesMod : Mod
{
    private static bool? s_combatExtendedLoaded = null;

    public static MoreInjuriesSettings Settings { get; private set; } = null!;

    internal static bool CombatExtendedLoaded =>
        s_combatExtendedLoaded ??= LoadedModManager.RunningModsListForReading.Any(static mod => mod.PackageIdPlayerFacing?.Equals("CETeam.CombatExtended") is true);

    public MoreInjuriesMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<MoreInjuriesSettings>();
        Harmony harmony = new("Th3Fr3d.MoreInjuries");
        harmony.PatchAll();
    }

    private Vector2 _scrollPosition;
    private float _contentHeight;

    public override void DoSettingsWindowContents(Rect canvas)
    {
        // Give Listing_Standard room for every group without automatic columns: measure the content,
        // then use that measurement for scrolling on the next frame.
        Rect view = new(canvas.x, canvas.y, canvas.width, canvas.height);
        Rect content = new(0f, 0f, canvas.width - 25f, Mathf.Max(_contentHeight, canvas.height));
        _scrollPosition.y = Mathf.Clamp(_scrollPosition.y, 0f, Mathf.Max(0f, _contentHeight - canvas.height));
        Widgets.BeginScrollView(view, ref _scrollPosition, content);
        Listing_Standard list = new();
        list.Begin(new Rect(0f, 0f, content.width, 1_000_000f));
        GameFont previousFont = Text.Font;
        Text.Font = GameFont.Small;
        MoreInjuriesSettingsUi.Draw(list, Settings);
        _contentHeight = list.CurHeight;
        list.End();
        Text.Font = previousFont;
        Widgets.EndScrollView();
        base.DoSettingsWindowContents(canvas);
    }

    public override string SettingsCategory() => "More Injuries (Continued)";
}
