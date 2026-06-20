using DivaniMods.Assets;
using Il2CppInterop.Runtime.Attributes;
using MiraAPI.Patches.Stubs;
using MiraAPI.Roles;
using TownOfUs.Extensions;
using TownOfUs.Modules;
using TownOfUs.Modules.Components;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace DivaniMods.Roles.Crewmate.CrewmatePower;

public sealed class DreamerRole(IntPtr cppPtr)
    : CrewmateRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IDoomable
{
    private MeetingMenu? meetingMenu;

    private GuesserMenu? dreamMenu;

    public string RoleName => "Dreamer";
    public string RoleDescription => "Reimagine fellow Crewmates!";
    public string RoleLongDescription => "Dream other players to become the roles you desire. Your dream fails if it targets a Non-Crewmate.";
    public Color RoleColor => new Color(0.5f, 0.3f, 0.1f); //what even is this color type
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;
    public RoleAlignment RoleAlignment => RoleAlignment.CrewmatePower;

    public override bool IsAffectedByComms => false;

    public DoomableType DoomHintType => DoomableType.Perception;

    public string GetAdvancedDescription() => RoleLongDescription + MiscUtils.AppendOptionsText(GetType());

    public int DreamTargetId;

    public RoleBehaviour? dreamTargetDreamRole;
    public RoleBehaviour? dreamTargetOriginalRole;


    public CustomRoleConfiguration Configuration => new(this)
    {
        Icon = DivaniAssets.DreamerIcon,
        IntroSound = DivaniAssets.DreamerIntroSound,
    };

    public override void Initialize(PlayerControl player)
    {
        RoleBehaviourStubs.Initialize(this, player);

        if (Player.AmOwner)
        {
            DreamTargetId = byte.MaxValue;

            meetingMenu = new MeetingMenu(
                this,
                OpenDreamMenu,
                "Dream",
                MeetingAbilityType.Click,
                DivaniAssets.DreamerMeetingDream,
                exemption: IsExempt,
                position: new Vector3(-0.35f, 0f, -3f));
        }
    }

    [HideFromIl2Cpp]
    public bool IsExempt(PlayerVoteArea voteArea)
    {
        // hide on dreamer
        if (voteArea == null || voteArea.TargetPlayerId == Player.PlayerId)
        {
            return true;
        }

        // hide on dreaming players

        // hide on insomniac players
        return false;
    }

    [HideFromIl2Cpp]
    public void OpenDreamMenu(PlayerVoteArea voteArea, MeetingHud meeting)
    {
        if (meeting.state == MeetingHud.VoteStates.Discussion || IsExempt(voteArea))
        {
            return;
        }

        if (Minigame.Instance)
        {
            return;
        }

        var dreamTarget = GameData.Instance.GetPlayerById(voteArea.TargetPlayerId)?.Object;
        if (dreamTarget == null)
        {
            return;
        }

        dreamMenu = GuesserMenu.Create();
        dreamMenu.Begin(IsRoleValid, OnRoleSelected);
    }

    public static bool IsRoleValid(RoleBehaviour role)
    {
        //
        return true;
    }

    public void OnRoleSelected(RoleBehaviour role)
    {
        if (dreamMenu != null)
        {
            dreamMenu.Close();
        }

        dreamTargetDreamRole = (RoleBehaviour?)RoleId.Get(role.GetType());
    }
}
    
