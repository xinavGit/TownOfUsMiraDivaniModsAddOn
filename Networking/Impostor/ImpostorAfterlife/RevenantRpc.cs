using MiraAPI.Modifiers;
using MiraAPI.Networking;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using DivaniMods.Roles.Impostor.ImpostorAfterlife;
using TownOfUs.Events;
using TownOfUs.Modifiers;
using TownOfUs.Modules.Localization;
using TownOfUs.Utilities;

namespace DivaniMods.Networking.Impostor.ImpostorAfterlife;

public static class RevenantRpc
{
    [MethodRpc((uint)DivaniRpcCalls.RevenantKill, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcRevenantKill(PlayerControl source, PlayerControl target)
    {
        if (LobbyBehaviour.Instance)
        {
            MiscUtils.RunAnticheatWarning(source);
            return;
        }

        if (source == null || target == null || !source.HasDied() || target.HasDied())
        {
            return;
        }

        if (source.Data?.Role is not RevenantRole)
        {
            return;
        }

        source.AddModifier<IndirectAttackerModifier>(true);

        DeathHandlerModifier.UpdateDeathHandlerImmediate(
            target,
            TouLocale.Get("DiedToRevenant"),
            DeathEventHandlers.CurrentRound,
            DeathHandlerOverride.SetTrue,
            TouLocale.GetParsed("DiedByStringBasic").Replace("<player>", source.Data.PlayerName),
            lockInfo: DeathHandlerOverride.SetTrue);
        DeathHandlerModifier.UpdateDeathHandlerImmediate(source, "null", -1, DeathHandlerOverride.SetFalse,
            lockInfo: DeathHandlerOverride.SetTrue);

        source.CustomMurder(target, MurderResultFlags.Succeeded);
    }
}
