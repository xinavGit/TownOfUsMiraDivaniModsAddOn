using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Modifiers;
using MiraAPI.Modifiers.Types;
using MiraAPI.Utilities;
using Reactor.Utilities.Extensions;
using DivaniMods.Buttons.Neutral.NeutralOutlier;
using DivaniMods.Modules.Duelist;
using DivaniMods.Options;
using DivaniMods.Roles.Neutral.NeutralOutlier;
using TownOfUs.Modifiers;
using TownOfUs.Utilities;
using TownOfUs.Utilities.Appearances;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DivaniMods.Modifiers.Neutral.NeutralOutlier;

// Hidden modifier carried by both duellists for the duration of a duel. It is networked
// (added inside the start RPC on every client) so that each client can render the pair
// differently for the local observer:
//   - the duellist sees themselves and the target normally,
//   - the target sees the duellist camouflaged (hidden identity),
//   - everyone else sees neither of them (invisible),
//   - and each participant has all non-participants hidden on their own screen.
// It also extends DisabledModifier, so every other ability/button is disabled for the
// duration of the duel (only the DuelFightButton bypasses that).
public sealed class DuelModifier(byte opponentId, bool isDuelist, Vector2 returnPos)
    : DisabledModifier, IVisualAppearance
{
    public override string ModifierName => "In Duel";
    public override bool HideOnUi => true;
    public override bool AutoStart => true;
    public override bool RemoveOnComplete => false;
    public override float Duration => 600f;
    public bool VisualPriority => true;

    [HideFromIl2Cpp] public bool IsHiddenFromList => true;

    public byte OpponentId { get; } = opponentId;
    public bool IsDuelist { get; } = isDuelist;
    public Vector2 ReturnPos { get; } = returnPos;

    private ArrowBehaviour? _arrow;
    private readonly HashSet<byte> _hidden = new();

    private static float Speed => OptionGroupSingleton<DuelistOptions>.Instance.DuelSpeed.Value;

    public VisualAppearance GetVisualAppearance()
    {
        var app = new VisualAppearance(Player.GetDefaultAppearance(), TownOfUsAppearances.PlayerNameOnly);

        // Both duellists get the duel speed boost.
        app.Speed = Speed;

        var observer = PlayerControl.LocalPlayer;
        if (observer == null || observer.PlayerId == Player.PlayerId)
        {
            return app; // see yourself normally
        }

        if (observer.PlayerId == OpponentId)
        {
            if (IsDuelist)
            {
                ApplyCamo(app); // duellist looks camouflaged to its target
            }
            return app;
        }

        ApplyInvisible(app); // invisible to everyone outside the duel
        return app;
    }

    private static void ApplyCamo(VisualAppearance app)
    {
        app.HatId = "hat_NoHat";
        app.SkinId = "skin_None";
        app.VisorId = "visor_EmptyVisor";
        app.PetId = "pet_EmptyPet";
        app.PlayerName = string.Empty;
        app.NameVisible = false;
        app.PlayerMaterialColor = Color.grey;
    }

    private static void ApplyInvisible(VisualAppearance app)
    {
        app.HatId = string.Empty;
        app.SkinId = string.Empty;
        app.VisorId = string.Empty;
        app.PetId = string.Empty;
        app.PlayerName = string.Empty;
        app.NameVisible = false;
        app.RendererColor = Color.clear;
        app.NameColor = Color.clear;
        app.ColorBlindTextColor = Color.clear;
    }

    public override void OnActivate()
    {
        base.OnActivate();
        Player.RawSetAppearance(this);

        if (Player.AmOwner)
        {
            _arrow = MiscUtils.CreateArrow(Player.transform, DuelistRole.DuelistColor);
        }
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        var opponent = MiscUtils.PlayerById(OpponentId);
        if (opponent == null || opponent.Data == null || opponent.Data.Disconnected)
        {
            if (Player.AmOwner)
            {
                DuelManager.AbortDuel(Player);
            }
            return;
        }

        // Reassert appearance through a mushroom mix-up, same as the swooper does.
        var mushroom = Object.FindObjectOfType<MushroomMixupSabotageSystem>();
        if (mushroom && mushroom.IsActive)
        {
            Player.RawSetAppearance(this);
        }

        if (!Player.AmOwner)
        {
            return;
        }

        if (_arrow != null && opponent.transform != null)
        {
            _arrow.target = opponent.transform.position;
            _arrow.Update();
        }

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.PlayerId == Player.PlayerId || p.PlayerId == OpponentId)
            {
                continue;
            }
            SetHidden(p, true);
            _hidden.Add(p.PlayerId);
        }
    }

    public override void OnDeactivate()
    {
        base.OnDeactivate();

        if (_arrow != null && !_arrow.IsDestroyedOrNull())
        {
            _arrow.gameObject.Destroy();
            _arrow.Destroy();
        }
        _arrow = null;

        foreach (var id in _hidden)
        {
            var p = MiscUtils.PlayerById(id);
            if (p != null)
            {
                SetHidden(p, false);
            }
        }
        _hidden.Clear();

        Player?.ResetAppearance();
    }

    // Keep the duel modifier through death so AfterMurder can still resolve the duel
    // (read the opponent/return position). It is removed explicitly when the duel ends.
    public override void OnDeath(DeathReason reason)
    {
    }

    [HideFromIl2Cpp]
    private static void SetHidden(PlayerControl p, bool hidden)
    {
        var a = hidden ? 0f : 1f;
        var cos = p.cosmetics;
        if (cos == null)
        {
            return;
        }

        if (cos.currentBodySprite != null && cos.currentBodySprite.BodySprite != null)
        {
            cos.currentBodySprite.BodySprite.color = cos.currentBodySprite.BodySprite.color.SetAlpha(a);
        }

        p.SetHatAndVisorAlpha(a);

        if (cos.skin != null && cos.skin.layer != null)
        {
            cos.skin.layer.color = cos.skin.layer.color.SetAlpha(a);
        }

        if (cos.currentPet != null)
        {
            foreach (var rend in cos.currentPet.renderers)
            {
                rend.color = rend.color.SetAlpha(a);
            }
            foreach (var shadow in cos.currentPet.shadows)
            {
                shadow.color = shadow.color.SetAlpha(a);
            }
        }

        cos.ToggleNameVisible(!hidden);
    }
}
