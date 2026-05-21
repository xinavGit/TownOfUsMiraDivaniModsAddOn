using System;
using System.Collections.Generic;
using System.Linq;
using MiraAPI.Modifiers;
using DivaniMods.Modifiers.Crewmate;

namespace DivaniMods.Utilities;

public static class DivaniNegativeEffects
{
    private static readonly List<Action<PlayerControl, ModifierComponent>> Removers =
    [
        Remover<BloodyKillerFootstepsModifier>(),
    ];

    public static void CleanseAll(PlayerControl player)
    {
        if (player == null)
        {
            return;
        }

        var comp = player.GetModifierComponent();
        if (comp == null)
        {
            return;
        }

        foreach (var remove in Removers)
        {
            remove(player, comp);
        }
    }

    private static Action<PlayerControl, ModifierComponent> Remover<T>()
        where T : BaseModifier
    {
        return (player, comp) =>
        {
            foreach (var modifier in player.GetModifiers<T>().ToArray())
            {
                comp.RemoveModifier(modifier);
            }
        };
    }
}
