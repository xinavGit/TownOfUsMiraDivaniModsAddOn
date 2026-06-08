using HarmonyLib;
using MiraAPI.Modifiers;
using DivaniMods.Modifiers.Game.Crewmate;
using TownOfUs.Modifiers.Game;
using TownOfUs.Roles.Crewmate;

namespace DivaniMods.Patches;
[HarmonyPatch(typeof(AssassinModifier), "IsModifierValid")]
public static class IncompetentAssassinModifierGuessPatch
{
    public static void Postfix(BaseModifier modifier, ref bool __result)
    {
        if (__result && modifier is IncompetentModifier)
        {
            __result = false;
        }
    }
}

[HarmonyPatch(typeof(VigilanteRole), "IsModifierValid")]
public static class IncompetentVigilanteModifierGuessPatch
{
    public static void Postfix(BaseModifier modifier, ref bool __result)
    {
        if (__result && modifier is IncompetentModifier)
        {
            __result = false;
        }
    }
}
