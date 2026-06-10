using MiraAPI.Modifiers;
using MiraAPI.Networking;
using MiraAPI.Utilities.Assets;
using Reactor.Utilities.Extensions;
using DivaniMods.Modifiers.Neutral.NeutralOutlier;
using DivaniMods.Roles.Neutral.NeutralOutlier;
using TownOfUs.Assets;
using TownOfUs.Buttons;
using TownOfUs.Utilities;
using UnityEngine;

namespace DivaniMods.Buttons.Neutral.NeutralOutlier;

// The only kill ability the Duelist has: usable strictly while in a duel, only against the
// current opponent. Gated by the hidden DuelModifier so the duel target gets it too.
public sealed class DuelFightButton : TownOfUsTargetButton<PlayerControl>, IKillButton
{
    public override string Name => "Strike";
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;
    public override Color TextOutlineColor => DuelistRole.DuelistColor;
    public override LoadableAsset<Sprite> Sprite => TouAssets.KillSprite;
    public override bool ZeroIsInfinite { get; set; } = true;
    public override float Cooldown => 0f;
    public override float InitialCooldown => 0f;

    public override bool Enabled(RoleBehaviour? role)
    {
        var lp = PlayerControl.LocalPlayer;
        return lp != null && !lp.HasDied() && lp.HasModifier<DuelModifier>();
    }

    public override PlayerControl? GetTarget()
    {
        var lp = PlayerControl.LocalPlayer;
        if (lp == null || !lp.TryGetModifier<DuelModifier>(out var mod))
        {
            return null;
        }

        var opponent = MiscUtils.PlayerById(mod.OpponentId);
        return opponent != null && IsTargetValid(opponent) ? opponent : null;
    }

    public override bool IsTargetValid(PlayerControl? target)
    {
        if (target == null || target.HasDied())
        {
            return false;
        }

        var lp = PlayerControl.LocalPlayer;
        if (lp != null && lp.TryGetModifier<DuelModifier>(out var mod))
        {
            return target.PlayerId == mod.OpponentId;
        }
        return false;
    }

    public override void FixedUpdateHandler(PlayerControl playerControl)
    {
        base.FixedUpdateHandler(playerControl);

        var newTarget = GetTarget();
        if (newTarget != Target)
        {
            SetOutline(false);
        }
        Target = IsTargetValid(newTarget) ? newTarget : null;
        SetOutline(true);
    }

    public override void SetOutline(bool active)
    {
        if (Target != null && !PlayerControl.LocalPlayer.HasDied())
        {
            Target.cosmetics.currentBodySprite.BodySprite.SetOutline(active ? DuelistRole.DuelistColor : null);
        }
    }

    // Bypass the DisabledModifier gates (the duel modifier is a DisabledModifier, which
    // disables every other ability) so only this button stays usable during a duel.
    public override bool CanUse()
    {
        var lp = PlayerControl.LocalPlayer;
        if (lp == null || lp.HasDied())
        {
            return false;
        }
        if (HudManager.Instance.Chat.IsOpenOrOpening || MeetingHud.Instance)
        {
            return false;
        }
        if (!lp.CanMove)
        {
            return false;
        }
        return Target != null && Timer <= 0;
    }

    public override void ClickHandler()
    {
        if (!CanClick())
        {
            return;
        }
        OnClick();
        Timer = Cooldown;
    }

    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }
        PlayerControl.LocalPlayer.RpcCustomMurder(Target, MeetingCheck.OutsideMeeting);
    }
}
