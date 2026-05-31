using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities.Assets;
using DivaniMods.Assets;
using DivaniMods.Options;
using TownOfUs.Buttons;
using UnityEngine;

namespace DivaniMods.Buttons.Crewmate.CrewmateSupport;

public class UsePortalButton : TownOfUsButton
{
    public override string Name => "Use Portal";
    public override float Cooldown => OptionGroupSingleton<PortalmakerOptions>.Instance.UsePortalCooldown.Value;
    public override float InitialCooldown => 0f;
    public override float EffectDuration => 0f;
    public override int MaxUses => 0;
    // MaxUses=0 must read as unlimited, not "0 left" (which would grey the button forever).
    public override bool ZeroIsInfinite { get; set; } = true;
    public override LoadableAsset<Sprite> Sprite => DivaniAssets.UsePortalButton;
    public override ButtonLocation Location => ButtonLocation.BottomLeft;
    public override Color TextOutlineColor => new Color(0.047f, 0.420f, 0.961f);
    public override BaseKeybind Keybind => Keybinds.TertiaryAction;

    private static bool PortalsUsableNow(PlayerControl? player)
    {
        if (player == null || player.Data == null || player.Data.IsDead) return false;
        if (!PortalManager.BothPortalsPlaced) return false;
        if (OptionGroupSingleton<PortalmakerOptions>.Instance.EnableAfterFirstMeeting &&
            !PortalManager.PortalsUnlocked) return false;
        return PortalManager.IsNearPortal(player.GetTruePosition());
    }

    public override bool Enabled(RoleBehaviour? role) => true;

    public override void SetActive(bool visible, RoleBehaviour role)
    {
        Button?.ToggleVisible(visible && Enabled(role) && PortalsUsableNow(PlayerControl.LocalPlayer));
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        if (MeetingHud.Instance)
        {
            return;
        }

        var hudActive = HudManager.Instance.UseButton.isActiveAndEnabled ||
                        HudManager.Instance.PetButton.isActiveAndEnabled;
        Button?.gameObject.SetActive(hudActive && PortalsUsableNow(PlayerControl.LocalPlayer));
    }

    public override bool CanUse()
    {
        var player = PlayerControl.LocalPlayer;
        if (!PortalsUsableNow(player)) return false;
        if (PlayerTask.PlayerHasTaskOfType<IHudOverrideTask>(player)) return false;
        if (!PortalManager.CanUsePortal(player.PlayerId)) return false;
        return base.CanUse();
    }

    protected override void OnClick()
    {
        var player = PlayerControl.LocalPlayer;
        if (player == null) return;

        var position = player.GetTruePosition();
        var destination = PortalManager.GetDestination(position);

        if (!destination.HasValue)
        {
            return;
        }

        PortalManager.RpcUsePortal(player, destination.Value.x, destination.Value.y);
        PortalManager.SyncPortalButtonCooldowns();
    }
}
