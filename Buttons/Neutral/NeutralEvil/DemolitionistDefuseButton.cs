using System.Collections;
using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities.Assets;
using Reactor.Utilities;
using DivaniMods.Assets;
using DivaniMods.Options;
using DivaniMods.Patches;
using DivaniMods.Roles.Neutral.NeutralEvil;
using TownOfUs.Buttons;
using UnityEngine;

namespace DivaniMods.Buttons.Neutral.NeutralEvil;

public class DemolitionistDefuseButton : TownOfUsButton
{
    public override string Name => "Defuse";
    public override float Cooldown => 1f;
    public override float InitialCooldown => 0f;
    public override float EffectDuration => OptionGroupSingleton<DemolitionistOptions>.Instance.IsTimedSabotageStyle
        ? OptionGroupSingleton<DemolitionistOptions>.Instance.DefuseTime.Value
        : 0f;
    public override int MaxUses => 0;
    public override bool ZeroIsInfinite { get; set; } = true;
    public override LoadableAsset<Sprite> Sprite => DivaniAssets.DemolitionistDefuseButton;
    public override ButtonLocation Location { get; set; } = ButtonLocation.BottomLeft;
    public override Color TextOutlineColor => DemolitionistRole.DemolitionistColor;
    public override BaseKeybind Keybind => Keybinds.TertiaryAction;

    public static DemolitionistDefuseButton? Instance { get; set; }

    private bool _isDefusing;

    public static bool IsLocalDefusing =>
        (Instance != null && Instance._isDefusing)
        || DemolitionistNumpad.Controller.DefuseInProgress;

    public override bool Enabled(RoleBehaviour? role)
    {
        Instance = this;
        return role != null;
    }
    private static bool DefuseVisibleNow()
    {
        var player = PlayerControl.LocalPlayer;
        if (player == null || player.Data == null || player.Data.IsDead) return false;
        if (!DemolitionistSabotageState.IsActive) return false;
        return DemolitionistSabotageState.IsLocalPlayerAtPlantedConsole();
    }

    public override void SetActive(bool visible, RoleBehaviour role)
    {
        Button?.ToggleVisible(visible && Enabled(role) && DefuseVisibleNow());
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        if (MeetingHud.Instance)
        {
            return;
        }

        var hudActive = HudManager.Instance.UseButton.isActiveAndEnabled ||
                        HudManager.Instance.PetButton.isActiveAndEnabled;
        Button?.gameObject.SetActive(hudActive && DefuseVisibleNow());
    }

    public override bool CanUse()
    {
        var player = PlayerControl.LocalPlayer;
        if (player == null || player.Data == null || player.Data.IsDead) return false;
        if (MeetingHud.Instance || ExileController.Instance) return false;
        if (!DemolitionistSabotageState.IsActive) return false;
        if (DemolitionistNumpad.Controller.InProgress) return false;
        if (_isDefusing) return false;
        if (!DemolitionistSabotageState.IsLocalPlayerAtPlantedConsole()) return false;

        return base.CanUse();
    }

    public override void ClickHandler()
    {
        if (!CanClick())
        {
            return;
        }

        OnClick();
        Button?.SetDisabled();
    }

    protected override void OnClick()
    {
        var player = PlayerControl.LocalPlayer;
        if (player == null) return;
        if (!DemolitionistSabotageState.IsActive) return;
        if (!DemolitionistSabotageState.IsLocalPlayerAtPlantedConsole()) return;
        if (_isDefusing) return;

        if (!OptionGroupSingleton<DemolitionistOptions>.Instance.IsTimedSabotageStyle)
        {
            Coroutines.Start(DefuseNumpadCoroutine(player));
            return;
        }

        Coroutines.Start(DefuseTimedCoroutine(player));
    }

    private IEnumerator DefuseTimedCoroutine(PlayerControl player)
    {
        _isDefusing = true;
        var defuseTime = EffectDuration;
        var colorHex = ColorUtility.ToHtmlStringRGB(DemolitionistRole.DemolitionistColor);

        EffectActive = true;
        Timer = defuseTime;
        Button?.OverrideText("DEFUSING");

        MiraAPI.Utilities.Helpers.CreateAndShowNotification(
            $"<b><color=#{colorHex}>Defusing...</color></b>",
            Color.white,
            new Vector3(0f, 1f, -20f),
            spr: DivaniAssets.DemolitionistIcon.LoadAsset());

        var elapsed = 0f;
        while (elapsed < defuseTime)
        {
            if (player == null || player.Data == null || player.Data.IsDead)
            {
                AbortDefuse();
                yield break;
            }

            if (!DemolitionistSabotageState.IsActive)
            {
                AbortDefuse();
                yield break;
            }

            if (!DemolitionistSabotageState.IsLocalPlayerAtPlantedConsole())
            {
                MiraAPI.Utilities.Helpers.CreateAndShowNotification(
                    $"<b><color=#{colorHex}>Defuse aborted — too far from sabotage!</color></b>",
                    Color.white,
                    new Vector3(0f, 1f, -20f),
                    spr: DivaniAssets.DemolitionistIcon.LoadAsset());
                AbortDefuse();
                yield break;
            }

            elapsed += Time.deltaTime;
            Timer = defuseTime - elapsed;
            if (Button != null)
            {
                Button.SetFillUp(Timer, defuseTime);
            }

            yield return null;
        }

        if (player == null || player.Data == null || player.Data.IsDead)
        {
            AbortDefuse();
            yield break;
        }

        if (!DemolitionistSabotageState.IsActive)
        {
            AbortDefuse();
            yield break;
        }

        if (!DemolitionistSabotageState.IsLocalPlayerAtPlantedConsole())
        {
            MiraAPI.Utilities.Helpers.CreateAndShowNotification(
                $"<b><color=#{colorHex}>Defuse aborted — too far from sabotage!</color></b>",
                Color.white,
                new Vector3(0f, 1f, -20f),
                spr: DivaniAssets.DemolitionistIcon.LoadAsset());
            AbortDefuse();
            yield break;
        }

        DemolitionistSabotageState.RpcDefuseSabotage(player, player.PlayerId);
        EffectActive = false;
        Timer = Cooldown;
        _isDefusing = false;
        Button?.OverrideText(Name.ToUpperInvariant());
    }

    private IEnumerator DefuseNumpadCoroutine(PlayerControl player)
    {
        _isDefusing = true;

        if (!DemolitionistNumpad.Controller.OpenDefuse(player))
        {
            AbortDefuse();
            yield break;
        }

        while (DemolitionistNumpad.Controller.InProgress)
        {
            if (player == null || player.Data == null || player.Data.IsDead
                || !DemolitionistSabotageState.IsActive)
            {
                AbortDefuse();
                yield break;
            }

            yield return null;
        }

        EffectActive = false;
        Timer = Cooldown;
        _isDefusing = false;
    }

    private void AbortDefuse()
    {
        if (DemolitionistNumpad.Controller.InProgress)
        {
            DemolitionistNumpad.Controller.CancelActive();
        }

        EffectActive = false;
        Timer = Cooldown;
        _isDefusing = false;
        Button?.OverrideText(Name.ToUpperInvariant());
    }
}
