using System.Collections;
using System.Collections.Generic;
using MiraAPI.Modifiers;
using Reactor.Utilities;
using DivaniMods.Assets;
using DivaniMods.Modifiers.Neutral.NeutralOutlier;
using DivaniMods.Roles.Neutral.NeutralOutlier;
using TownOfUs.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DivaniMods.Modules.Duelist;

public static class DuelManager
{
    private static readonly Dictionary<byte, int> Wins = new();
    private static readonly Dictionary<byte, int> Losses = new();

    public static int GetWins(byte playerId) => Wins.TryGetValue(playerId, out var v) ? v : 0;
    public static int GetLosses(byte playerId) => Losses.TryGetValue(playerId, out var v) ? v : 0;
    public static void AddWin(byte playerId) => Wins[playerId] = GetWins(playerId) + 1;
    public static void AddLoss(byte playerId) => Losses[playerId] = GetLosses(playerId) + 1;

    public static void ResetAll()
    {
        Wins.Clear();
        Losses.Clear();
    }

    // Picks the two closest rooms to the duellist (excluding the room they stand in), then
    // looks up the hardcoded duel coordinate for each. Unfilled rooms fall back to centre.
    public static bool TryGetDuelDestinations(PlayerControl duelist, PlayerControl target,
        out Vector2 duelistDest, out Vector2 targetDest)
    {
        duelistDest = duelist.GetTruePosition();
        targetDest = target.GetTruePosition();

        if (ShipStatus.Instance == null)
        {
            return false;
        }

        var from = duelist.GetTruePosition();
        var currentRoom = GetRoomAt(from);

        var rooms = new List<PlainShipRoom>();
        foreach (var room in ShipStatus.Instance.FastRooms.Values)
        {
            if (room == null || room.roomArea == null)
            {
                continue;
            }
            if (currentRoom != null && room.RoomId == currentRoom.RoomId)
            {
                continue;
            }
            rooms.Add(room);
        }

        if (rooms.Count < 2)
        {
            return false;
        }

        rooms.Sort((a, b) =>
            Vector2.Distance(from, a.roomArea.bounds.center)
                .CompareTo(Vector2.Distance(from, b.roomArea.bounds.center)));

        duelistDest = GetRoomDest(rooms[0]);
        targetDest = GetRoomDest(rooms[1]);
        return true;
    }

    private static PlainShipRoom? GetRoomAt(Vector2 pos)
    {
        foreach (var room in ShipStatus.Instance.FastRooms.Values)
        {
            if (room != null && room.roomArea != null && room.roomArea.OverlapPoint(pos))
            {
                return room;
            }
        }
        return null;
    }

    private static Vector2 GetRoomDest(PlainShipRoom room)
    {
        return DuelRoomPositions.TryGet(room.RoomId, out var pos)
            ? pos
            : room.roomArea.bounds.center;
    }

    // Normal duel end: notify both players, wait briefly, then send the survivor(s) home.
    // The loser's body (if they died) stays where it fell.
    public static void EndDuel(PlayerControl winner, PlayerControl loser, bool loserDied)
    {
        if (winner == null || loser == null)
        {
            return;
        }

        ShowResultNotifs(winner, loser);
        Coroutines.Start(CoEndDuel(winner, loser, loserDied));
    }

    private static IEnumerator CoEndDuel(PlayerControl winner, PlayerControl loser, bool loserDied)
    {
        yield return new WaitForSeconds(0.5f);

        if (winner.TryGetModifier<DuelModifier>(out var winnerMod))
        {
            TeleportBack(winner, winnerMod.ReturnPos);
        }

        if (!loserDied && loser.TryGetModifier<DuelModifier>(out var loserMod))
        {
            TeleportBack(loser, loserMod.ReturnPos);
        }

        RemoveDuel(winner);
        RemoveDuel(loser);
    }

    // Opponent disconnected or left mid-duel: quietly send the survivor home and clear up.
    public static void AbortDuel(PlayerControl player)
    {
        if (player == null || !player.TryGetModifier<DuelModifier>(out var mod))
        {
            return;
        }

        var opponent = MiscUtils.PlayerById(mod.OpponentId);
        TeleportBack(player, mod.ReturnPos);
        RemoveDuel(player);
        if (opponent != null)
        {
            RemoveDuel(opponent);
        }
    }

    private static void ShowResultNotifs(PlayerControl winner, PlayerControl loser)
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null)
        {
            return;
        }

        var hex = ColorUtility.ToHtmlStringRGB(DuelistRole.DuelistColor);
        var icon = DivaniAssets.DuelistIcon.LoadAsset();

        if (local.PlayerId == winner.PlayerId)
        {
            MiraAPI.Utilities.Helpers.CreateAndShowNotification(
                $"<b><color=#{hex}>You won the duel!</color></b>", Color.white,
                new Vector3(0f, 1f, -20f), spr: icon);
        }
        else if (local.PlayerId == loser.PlayerId)
        {
            MiraAPI.Utilities.Helpers.CreateAndShowNotification(
                $"<b><color=#{hex}>You lost the duel!</color></b>", Color.white,
                new Vector3(0f, 1f, -20f), spr: icon);
        }
    }

    private static void TeleportBack(PlayerControl player, Vector2 pos)
    {
        if (player == null || player.HasDied())
        {
            return;
        }

        player.MyPhysics.ResetMoveState();
        player.transform.position = pos;
        if (player.AmOwner)
        {
            player.NetTransform.RpcSnapTo(pos);
            MiscUtils.SnapPlayerCamera(player);
        }
    }

    private static void RemoveDuel(PlayerControl player)
    {
        if (player != null && player.TryGetModifier<DuelModifier>(out var mod))
        {
            player.RemoveModifier(mod);
        }
    }
}
