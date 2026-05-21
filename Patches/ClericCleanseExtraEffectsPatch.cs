using HarmonyLib;
using DivaniMods.Utilities;
using TownOfUs.Modifiers.Crewmate;

namespace DivaniMods.Patches;
public static class ClericCleanseExtraEffectsPatch
{
    [HarmonyPatch(typeof(ClericCleanseModifier), "CleansePlayer")]
    private static class CleansePlayerPatch
    {
        private static void Postfix(ClericCleanseModifier __instance)
        {
            DivaniNegativeEffects.CleanseAll(__instance.Player);
        }
    }
}
