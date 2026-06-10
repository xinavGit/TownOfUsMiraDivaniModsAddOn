using MiraAPI.Modifiers;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using DivaniMods.Modifiers.Neutral.NeutralOutlier;
using DivaniMods.Roles.Neutral.NeutralOutlier;
using TownOfUs.Utilities;
using UnityEngine;

namespace DivaniMods.Networking.Neutral.NeutralOutlier;

public static class DuelistRpc
{
    [MethodRpc((uint)DivaniRpcCalls.DuelistStartDuel, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcStartDuel(PlayerControl duelist, byte targetId,
        Vector2 duelistDest, Vector2 targetDest, Vector2 duelistReturn, Vector2 targetReturn)
    {
        if (duelist == null || duelist.Data == null || duelist.Data.Role is not DuelistRole)
        {
            return;
        }

        var target = MiscUtils.PlayerById(targetId);
        if (target == null || target.HasDied())
        {
            return;
        }

        if (duelist.TryGetComponent<ModifierComponent>(out var duelistComp))
        {
            duelistComp.AddModifier(new DuelModifier(targetId, true, duelistReturn));
        }
        if (target.TryGetComponent<ModifierComponent>(out var targetComp))
        {
            targetComp.AddModifier(new DuelModifier(duelist.PlayerId, false, targetReturn));
        }

        Teleport(duelist, duelistDest);
        Teleport(target, targetDest);
    }

    private static void Teleport(PlayerControl player, Vector2 dest)
    {
        if (player.inVent)
        {
            player.MyPhysics.ExitAllVents();
        }

        player.MyPhysics.ResetMoveState();
        player.transform.position = dest;

        if (player.AmOwner)
        {
            player.NetTransform.RpcSnapTo(dest);
            MiscUtils.SnapPlayerCamera(player);
        }
    }
}
