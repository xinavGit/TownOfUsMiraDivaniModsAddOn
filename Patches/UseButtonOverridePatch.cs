using HarmonyLib;
using MiraAPI.Hud;
using DivaniMods.Assets;
using DivaniMods.Buttons.Crewmate.CrewmateSupport;
using DivaniMods.Buttons.Neutral.NeutralEvil;
using DivaniMods.Roles.Neutral.NeutralEvil;
using UnityEngine;

namespace DivaniMods.Patches;

[HarmonyPatch]
public static class UseButtonOverridePatch
{
    private static readonly Color PortalLabelColor = new Color(0.047f, 0.420f, 0.961f);

    private enum Kind
    {
        None,
        Portal,
        Defuse,
    }

    private static Kind _active = Kind.None;
    private static Sprite? _savedSprite;
    private static bool _savedSpriteValid;
    private static string? _savedLabel;
    private static bool _savedLabelActive;
    private static bool _savedLabelValid;

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPostfix]
    public static void HudManagerUpdatePostfix(HudManager __instance)
    {
        PortalManager.UpdatePortalOutlines();
        DemolitionistSabotageState.UpdatePlantedConsoleOutline();

        var useButton = __instance != null ? __instance.UseButton : null;
        if (useButton == null)
        {
            return;
        }

        ApplyOverride(useButton);
    }

    [HarmonyPatch(typeof(UseButton), nameof(UseButton.SetTarget))]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPostfix]
    public static void UseButtonSetTargetPostfix(UseButton __instance)
    {
        if (__instance == null)
        {
            return;
        }

        ApplyOverride(__instance);
    }

    [HarmonyPatch(typeof(ConsoleJoystick), nameof(ConsoleJoystick.Update))]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPostfix]
    public static void ConsoleJoystickUpdatePostfix()
    {
        var hud = HudManager.Instance;
        var useButton = hud != null ? hud.UseButton : null;
        if (useButton == null)
        {
            return;
        }

        ApplyOverride(useButton);
    }

    [HarmonyPatch(typeof(ActionButton), nameof(ActionButton.SetDisabled))]
    [HarmonyPriority(Priority.First)]
    [HarmonyPrefix]
    public static bool ActionButtonSetDisabledPrefix(ActionButton __instance)
    {
        var hud = HudManager.Instance;
        if (hud == null || __instance != hud.UseButton)
        {
            return true;
        }

        return !WantsUseButtonEnabled();
    }

    private static bool WantsUseButtonEnabled()
    {
        switch (ComputeKind())
        {
            case Kind.Defuse:
                return !DemolitionistDefuseButton.IsLocalDefusing;
            case Kind.Portal:
                return true;
            default:
                return false;
        }
    }

    [HarmonyPatch(typeof(UseButton), nameof(UseButton.DoClick))]
    [HarmonyPriority(Priority.First)]
    [HarmonyPrefix]
    public static bool UseButtonDoClickPrefix()
    {
        switch (_active)
        {
            case Kind.Defuse:
                CustomButtonSingleton<DemolitionistDefuseButton>.Instance?.TriggerDefuseFromUseButton();
                return false;
            case Kind.Portal:
                CustomButtonSingleton<UsePortalButton>.Instance?.TriggerFromUseButton();
                return false;
            default:
                return true;
        }
    }

    private static void ApplyOverride(UseButton useButton)
    {
        var kind = ComputeKind();

        if (kind == Kind.None)
        {
            ClearOverride(useButton);
            return;
        }

        if (_active == Kind.None)
        {
            SaveOriginal(useButton);
        }

        _active = kind;

        var sprite = kind == Kind.Defuse
            ? DivaniAssets.DemolitionistDefuseButton.LoadAsset()
            : DivaniAssets.UsePortalButton.LoadAsset();

        var label = kind == Kind.Defuse ? "DEFUSE" : "USE PORTAL";
        var labelColor = kind == Kind.Defuse ? DemolitionistRole.DemolitionistColor : PortalLabelColor;

        var enabled = kind != Kind.Defuse || !DemolitionistDefuseButton.IsLocalDefusing;

        if (enabled)
        {
            useButton.SetEnabled();
            ForceVisualEnabled(useButton);
        }
        else
        {
            useButton.SetDisabled();
        }

        useButton.SetCoolDown(0f, 1f);

        if (useButton.graphic != null && sprite != null)
        {
            useButton.graphic.sprite = sprite;
            useButton.graphic.SetCooldownNormalizedUvs();
        }

        if (useButton.buttonLabelText != null)
        {
            useButton.buttonLabelText.gameObject.SetActive(true);
            useButton.buttonLabelText.text = label;
            useButton.buttonLabelText.color = labelColor;
            useButton.buttonLabelText.SetOutlineColor(labelColor);
        }
    }

    private static void ForceVisualEnabled(UseButton useButton)
    {
        var renderers = useButton.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in renderers)
        {
            if (sr == null)
            {
                continue;
            }

            sr.color = Palette.EnabledColor;
            if (sr.material != null)
            {
                sr.material.SetFloat("_Desat", 0f);
            }
        }
    }

    private static void SaveOriginal(UseButton useButton)
    {
        if (useButton.graphic != null)
        {
            _savedSprite = useButton.graphic.sprite;
            _savedSpriteValid = true;
        }

        if (useButton.buttonLabelText != null)
        {
            _savedLabel = useButton.buttonLabelText.text;
            _savedLabelActive = useButton.buttonLabelText.gameObject.activeSelf;
            _savedLabelValid = true;
        }
    }

    private static void ClearOverride(UseButton useButton)
    {
        if (_active == Kind.None)
        {
            return;
        }

        if (_savedSpriteValid && useButton.graphic != null)
        {
            useButton.graphic.sprite = _savedSprite;
            useButton.graphic.SetCooldownNormalizedUvs();
        }

        if (_savedLabelValid && useButton.buttonLabelText != null)
        {
            useButton.buttonLabelText.text = _savedLabel;
            useButton.buttonLabelText.gameObject.SetActive(_savedLabelActive);
        }

        useButton.SetDisabled();

        _savedSprite = null;
        _savedSpriteValid = false;
        _savedLabel = null;
        _savedLabelValid = false;
        _active = Kind.None;
    }

    private static Kind ComputeKind()
    {
        if (!ShipStatus.Instance || MeetingHud.Instance || ExileController.Instance)
        {
            return Kind.None;
        }

        if (DemolitionistDefuseButton.ShouldDriveUseButton())
        {
            return Kind.Defuse;
        }

        if (UsePortalButton.ShouldDriveUseButton())
        {
            return Kind.Portal;
        }

        return Kind.None;
    }
}
