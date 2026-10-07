# Skill Overhaul (TingXueMian.SkillOverhaul) Compatibility

This branch adds compatibility between [UIInfoSuite2Alt](https://github.com/dazuki/UIInfoSuite2Alt)
and **Skill Overhaul** (`TingXueMian.SkillOverhaul`, by TingXueMian).

## Background

Skill Overhaul uses Harmony to raise the five vanilla skill caps from level 10 to 20:

- It patches `Farmer.getUnmodifiedSkillLevel` (and friends), so the UI already reads
  levels 11-20 correctly — level-up notifications work.
- Cumulative XP keeps growing in the vanilla `experiencePoints[]` array past 15000.
- However, UIInfoSuite2Alt's curve lookup only knows vanilla levels 0-9 plus the
  Walk of Life / VPP extension frameworks → **the experience bar disappears above
  level 10**.

Additionally, the vanilla design only replaces the skill bar with the mastery bar
once the current skill is fully maxed. While leveling extended levels (11-19),
mastery progress is completely invisible — a shared blind spot for WoL / VPP / SO
alike. This branch fixes that display gap as well.

## Changes

| File | Type | Description |
|---|---|---|
| `Compatibility/Helpers/SkillOverhaulHelper.cs` | new | Reads SO's `config.json` at launch: `MaxLevel` and `ExperienceRequiredByLevel` (cumulative XP curve for levels 1-20) |
| `Compatibility/ApiManager.cs` | +1 line | `ModCompat.SkillOverhaul` constant |
| `Options/GmcmRegistration.cs` | +1 line | Call `SkillOverhaulHelper.Initialize()` on GameLaunched |
| `UIElements/Experience/ExperienceBar.cs` | modified | Curve lookup consults SO first; mastery progress shown as a stacked secondary bar |

## How it works

### 1. XP curve priority

```
GetExperienceRequiredToLevel(currentLevel)
  ├─ SO loaded → return SO's cumulative XP from its config (covers the whole
  │              1-20 curve, matching SO's own leveling logic; edits to the
  │              config take effect on next launch)
  └─ not loaded → vanilla 0-9 hardcoded → WoL → VPP → -1 (treated as maxed)
```

When SO is not loaded, the original logic is byte-for-byte unchanged (including
WoL/VPP support). If SO's config is missing or unparsable, the helper falls back
to SO's built-in defaults (20500 → 88000).

### 2. Mastery bar display (framework-agnostic)

| State | Primary bar | Mastery secondary bar |
|---|---|---|
| Mastery locked | skill bar | none |
| Unlocked, skill still leveling | skill bar | ✅ green mastery bar (stacked above) |
| Skill maxed, mastery < 5 | mastery bar (primary switch) | removed (no duplicate) |
| Mastery at 5, skill still leveling | skill bar | none |
| Both maxed | none | none |

Without any level-extension mod, the state "unlocked but not maxed" cannot exist,
so the new branch never triggers — zero impact for vanilla players. WoL's
"prestige requires mastery first" special case and VPP's configurable mastery
gate (10/15/20) are unaffected.

## Build & deploy

Requires the .NET SDK (8.0+):

```bash
dotnet build UIInfoSuite2Alt/UIInfoSuite2Alt.csproj
```

ModBuildConfig auto-detects the game folder and deploys to `Mods/UIInfoSuite2Alt/`.
(If Steam lives in a non-standard location, add `<GamePath>` to the csproj.)
The mod's own `config.json` (user settings) is never touched by deployment.

## Updating from upstream

```bash
git fetch origin
git rebase origin/main
dotnet build UIInfoSuite2Alt/UIInfoSuite2Alt.csproj
```

New files never conflict; the `ExperienceBar.cs` changes are concentrated in the
curve lookup and the mastery branch — if they ever conflict with an upstream
update, realign using the decision table above.
