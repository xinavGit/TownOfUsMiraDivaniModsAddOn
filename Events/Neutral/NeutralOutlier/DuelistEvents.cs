using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using DivaniMods.Modifiers.Neutral.NeutralOutlier;
using DivaniMods.Modules.Duelist;
using DivaniMods.Options;

namespace DivaniMods.Events.Neutral.NeutralOutlier;

public static class DuelistEvents
{
    [RegisterEvent]
    public static void OnBeforeMurder(BeforeMurderEvent evt)
    {
        var src = evt.Source;
        var tgt = evt.Target;
        if (src == null || tgt == null)
        {
            return;
        }

        var srcDuel = src.TryGetModifier<DuelModifier>(out var sm);
        var tgtDuel = tgt.TryGetModifier<DuelModifier>(out var tm);
        if (!srcDuel && !tgtDuel)
        {
            return;
        }

        // A duellist is isolated: outsiders can't touch the pair and the pair can't touch outsiders.
        if (srcDuel != tgtDuel)
        {
            evt.Cancel();
            return;
        }

        // Both duelling, but not each other (shouldn't happen with one duel - stay safe).
        if (sm!.OpponentId != tgt.PlayerId || tm!.OpponentId != src.PlayerId)
        {
            evt.Cancel();
            return;
        }

        // Victim is the duellist -> the duellist is losing this duel.
        if (tm.IsDuelist)
        {
            DuelManager.AddLoss(tgt.PlayerId);

            var lossesToDie = (int)OptionGroupSingleton<DuelistOptions>.Instance.DuelsLostToDie.Value;
            if (DuelManager.GetLosses(tgt.PlayerId) >= lossesToDie)
            {
                // Enough losses: let the kill through, the duellist dies for good (AfterMurder finishes up).
                return;
            }

            // Survives the loss: cancel the kill but the duel still ends.
            evt.Cancel();
            DuelManager.EndDuel(src, tgt, false);
        }

        // Otherwise the duellist is the killer (winning) -> allow it; AfterMurder handles the win.
    }

    [RegisterEvent]
    public static void OnAfterMurder(AfterMurderEvent evt)
    {
        var src = evt.Source;
        var tgt = evt.Target;
        if (src == null || tgt == null)
        {
            return;
        }

        if (!src.TryGetModifier<DuelModifier>(out var sm) || !tgt.HasModifier<DuelModifier>() ||
            sm.OpponentId != tgt.PlayerId)
        {
            return;
        }

        // src is the killer (alive winner), tgt is the dead loser. The loser's body stays put.
        if (sm.IsDuelist)
        {
            DuelManager.AddWin(src.PlayerId);
        }

        DuelManager.EndDuel(src, tgt, true);
    }

    [RegisterEvent]
    public static void OnStartMeeting(StartMeetingEvent _)
    {
        // Refresh the hidden duel modifiers so vision/appearance is clean for the meeting.
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p != null && p.TryGetModifier<DuelModifier>(out var mod))
            {
                p.RemoveModifier(mod);
            }
        }
    }

    [RegisterEvent]
    public static void OnRoundStart(RoundStartEvent evt)
    {
        if (evt.TriggeredByIntro)
        {
            DuelManager.ResetAll();
        }
    }
}
