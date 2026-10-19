using MoreInjuries.Integrations.CombatExtended;
using Verse;

namespace MoreInjuries.Initialization;

[StaticConstructorOnStartup]
internal static class CombatExtended_Initializer
{
    static CombatExtended_Initializer()
    {
        // Harmony may run CE's texture-loading static constructor while compiling a detour.
        // Startup constructors run on the main thread, after all mod constructors registered patches.
        CombatExtendedBridge.Initialize();
        CombatExtendedBridge.VerifyRegistration();
    }
}
