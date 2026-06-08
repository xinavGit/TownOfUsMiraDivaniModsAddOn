using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities.Assets;
using Reactor.Utilities;
using DivaniMods.Assets;
using DivaniMods.Options;
using DivaniMods.Roles.Crewmate.CrewmateSupport;
using System.Collections;
using TownOfUs.Buttons;
using UnityEngine;

namespace DivaniMods.Buttons.Crewmate.CrewmateSupport;

public class PlacePortalButton : TownOfUsButton
{
    public override string Name => "Place Portal";
    public override float Cooldown => OptionGroupSingleton<PortalmakerOptions>.Instance.PlacePortalCooldown.Value;
    public override float EffectDuration => OptionGroupSingleton<PortalmakerOptions>.Instance.PlacePortalDuration.Value;
    public override int MaxUses => 2;
    public override LoadableAsset<Sprite> Sprite => DivaniAssets.PlacePortalButton;
    public override ButtonLocation Location { get; set; } = ButtonLocation.BottomRight;
    public override Color TextOutlineColor => new Color(0.047f, 0.420f, 0.961f);
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;
    
    private bool _isPlacing;

    public override bool Enabled(RoleBehaviour? role)
    {
        return role is PortalmakerRole;
    }

    public override bool CanUse()
    {
        var player = PlayerControl.LocalPlayer;
        if (player == null || player.Data == null || player.Data.IsDead) return false;

        if (PlayerTask.PlayerHasTaskOfType<IHudOverrideTask>(player))
            return false;
        
        if (_isPlacing) return false;
        
        int placedCount = PortalManager.PortalsPlaced;
        SetUses(2 - placedCount);
        
        return placedCount < 2 && base.CanUse();
    }

    protected override void OnClick()
    {
        var player = PlayerControl.LocalPlayer;
        if (player == null) return;
        
        if (PortalManager.PortalsPlaced >= 2)
        {
            return;
        }
        
        if (_isPlacing) return;
        
        var capturedPosition = player.GetTruePosition();
        Coroutines.Start(PlacePortalCoroutine(player, capturedPosition));
    }
    
    private IEnumerator PlacePortalCoroutine(PlayerControl player, Vector2 capturedPosition)
    {
        _isPlacing = true;
        
        
        MiraAPI.Utilities.Helpers.CreateAndShowNotification(
            "<b><color=#6633CC>Placing portal...</color></b>",
            Color.white,
            new Vector3(0f, 1f, -20f),
            spr: DivaniAssets.PortalmakerIcon.LoadAsset());
        
        yield return new WaitForSeconds(EffectDuration);

        if (player == null || player.Data == null || player.Data.IsDead)
        {
            _isPlacing = false;
            yield break;
        }

        PortalManager.RpcPlacePortal(player, capturedPosition.x, capturedPosition.y);

        PlayPlacePortalSound();
        
        int portalNum = PortalManager.PortalsPlaced;
        var afterMeeting = OptionGroupSingleton<PortalmakerOptions>.Instance.EnableAfterFirstMeeting;
        string message = portalNum == 1
            ? "<b><color=#6633CC>Portal 1 placed! Place another portal to complete the link.</color></b>"
            : afterMeeting
                ? "<b><color=#6633CC>Portal 2 placed! Portals will be enabled after the next meeting.</color></b>"
                : "<b><color=#6633CC>Portal 2 placed! Portals are now active!</color></b>";
        
        MiraAPI.Utilities.Helpers.CreateAndShowNotification(
            message,
            Color.white,
            new Vector3(0f, 1f, -20f),
            spr: DivaniAssets.PortalmakerIcon.LoadAsset());
        
        _isPlacing = false;
    }
    
    private static void PlayPlacePortalSound()
    {
        if (!SoundManager.Instance) return;
        try
        {
            var clip = DivaniAssets.PlacePortalSound.LoadAsset();
            if (clip == null) return;
            SoundManager.Instance.PlaySound(clip, false, 1f);
        }
        catch (System.Exception ex)
        {
            DivaniPlugin.Instance.Log.LogWarning($"Portalmaker: place sfx failed: {ex.Message}");
        }
    }

}
