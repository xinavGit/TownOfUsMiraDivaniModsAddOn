# Divani Mods v1.1.1

**Recruiter joins the squad, plus meeting-safe Divani buttons.**

Divani Mods 1.1.1 adds **Recruiter** (Impostor Support), who converts one non-Impostor during the **first meeting** into a vanilla Impostor, and **Bloody** (Crewmate modifier), which leaves a **trail of red footprints** on the killer for a configurable time after they slay a Bloody crewmate. The source tree is tidier thanks to clearer folder layout, and Divani ability **keybinds** no longer fire during meetings or while chat is open. Abilities now inherit the Town Of Us Mira button stack (`TownOfUsButton` / `TownOfUsTargetButton`), with **Lockdown** chaining `base.CanUse()` so it follows the same guards.

## Added

### Roles

- **Recruiter** (Impostor Support): During the first meeting only, mark one valid non-Impostor on the vote board; when the meeting ends, they become a vanilla Impostor (optional **Impostor Assassin** follow-up via options).

### Modifiers

- **Bloody** (Crewmate): Killing a Bloody crewmate makes the killer leave **impostor-red footprints** for a configurable duration, with spacing by distance or time, fade length, size, and optional prints in vent areas.

## Improvements

- Tidier codebase layout (more structured folders).

## Bug fixes

- **Keybinds:** Divani ability keybinds could fire during meetings; they now respect meeting, chat, rewind, and related Town Of Us Mira button checks.

## Known bug(s)

- Uses left on Sentinel is not always correct (visual bug).
- Previous Plague Doctor is not removed from the winners screen when **Amnesiac**
  inherits PD (Town Of Us end-game display).

---
