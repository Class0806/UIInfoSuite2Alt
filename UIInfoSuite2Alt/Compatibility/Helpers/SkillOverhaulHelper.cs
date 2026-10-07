using System;
using System.IO;
using System.Text.Json;
using StardewModdingAPI;

namespace UIInfoSuite2Alt.Compatibility.Helpers;

/// <summary>Reads Skill Overhaul (TingXueMian.SkillOverhaul) extended skill config from disk.</summary>
internal static class SkillOverhaulHelper
{
  private static bool _initialized;
  private static bool _loaded;
  private static int _maxLevel = 20;

  // Cumulative XP required to reach each level (index = level). Mirrors Skill Overhaul's
  // ExperienceRequiredByLevel config, which also redefines levels 1-10 with vanilla defaults.
  private static readonly int[] _cumulativeXpByLevel = new int[21];

  /// <summary>Whether Skill Overhaul's extended levels are available.</summary>
  public static bool Loaded => _loaded;

  /// <summary>
  /// Cumulative XP required to complete the given level (i.e. reach the next one), or -1 when
  /// Skill Overhaul is unavailable or the level is at/beyond its max.
  /// </summary>
  public static int GetExperienceRequiredToLevel(int currentLevel)
  {
    if (!_initialized || !_loaded)
    {
      return -1;
    }

    int nextLevel = currentLevel + 1; // level 0 -> reach 1, ..., level 19 -> reach 20
    if (nextLevel < 1 || nextLevel > _maxLevel)
    {
      return -1;
    }

    return _cumulativeXpByLevel[nextLevel];
  }

  /// <summary>Reads the curve from Skill Overhaul's config.json. Call once on GameLaunched.</summary>
  public static void Initialize(IModHelper helper)
  {
    _initialized = false;
    _loaded = false;
    _maxLevel = 20;
    Array.Fill(_cumulativeXpByLevel, -1);

    if (!helper.ModRegistry.IsLoaded(ModCompat.SkillOverhaul))
    {
      return;
    }

    try
    {
      string? configPath = GetModConfigPath(helper, ModCompat.SkillOverhaul);

      if (configPath == null || !File.Exists(configPath))
      {
        ModEntry.MonitorObject.Log(
          "SkillOverhaulHelper: could not locate config.json, using default curve",
          LogLevel.Warn
        );
        LoadDefaultCurve();
        return;
      }

      ModEntry.MonitorObject.Log(
        $"SkillOverhaulHelper: reading config from {configPath}",
        LogLevel.Trace
      );

      string json = File.ReadAllText(configPath);
      using var doc = JsonDocument.Parse(json);
      JsonElement root = doc.RootElement;

      if (
        root.TryGetProperty("MaxLevel", out JsonElement maxLevel)
        && maxLevel.ValueKind == JsonValueKind.Number
      )
      {
        // Skill Overhaul itself clamps this to [10, 20]
        _maxLevel = Math.Clamp(maxLevel.GetInt32(), 10, 20);
      }

      if (
        root.TryGetProperty("ExperienceRequiredByLevel", out JsonElement curve)
        && curve.ValueKind == JsonValueKind.Object
      )
      {
        foreach (JsonProperty entry in curve.EnumerateObject())
        {
          if (
            int.TryParse(entry.Name, out int level)
            && level >= 1
            && level <= 20
            && entry.Value.ValueKind == JsonValueKind.Number
          )
          {
            _cumulativeXpByLevel[level] = Math.Max(0, entry.Value.GetInt32());
          }
        }

        // Skill Overhaul falls back to the last defined threshold below a missing level,
        // so carry the previous value forward (level 1 defaults to 0 when undefined)
        _cumulativeXpByLevel[1] = Math.Max(0, _cumulativeXpByLevel[1]);
        for (int level = 2; level <= 20; ++level)
        {
          if (_cumulativeXpByLevel[level] < 0)
          {
            _cumulativeXpByLevel[level] = _cumulativeXpByLevel[level - 1];
          }
        }

        _loaded = true;
        _initialized = true;

        ModEntry.MonitorObject.Log(
          $"SkillOverhaulHelper: maxLevel={_maxLevel}, "
            + $"xpToReach{_maxLevel}={_cumulativeXpByLevel[_maxLevel]}",
          LogLevel.Trace
        );
      }
      else
      {
        ModEntry.MonitorObject.Log(
          "SkillOverhaulHelper: no ExperienceRequiredByLevel in config, using default curve",
          LogLevel.Warn
        );
        LoadDefaultCurve();
      }
    }
    catch (Exception ex)
    {
      ModEntry.MonitorObject.Log(
        $"SkillOverhaulHelper: failed to read config.json, {ex.Message}",
        LogLevel.Warn
      );
    }
  }

  /// <summary>Skill Overhaul's built-in defaults (vanilla 1-10, then +8000/level up to 20).</summary>
  private static void LoadDefaultCurve()
  {
    _maxLevel = 20;
    _cumulativeXpByLevel[1] = 100;
    _cumulativeXpByLevel[2] = 380;
    _cumulativeXpByLevel[3] = 770;
    _cumulativeXpByLevel[4] = 1300;
    _cumulativeXpByLevel[5] = 2150;
    _cumulativeXpByLevel[6] = 3300;
    _cumulativeXpByLevel[7] = 4800;
    _cumulativeXpByLevel[8] = 6900;
    _cumulativeXpByLevel[9] = 10000;
    _cumulativeXpByLevel[10] = 15000;
    _cumulativeXpByLevel[11] = 20500;
    _cumulativeXpByLevel[12] = 26500;
    _cumulativeXpByLevel[13] = 33000;
    _cumulativeXpByLevel[14] = 40000;
    _cumulativeXpByLevel[15] = 48000;
    _cumulativeXpByLevel[16] = 56000;
    _cumulativeXpByLevel[17] = 64000;
    _cumulativeXpByLevel[18] = 72000;
    _cumulativeXpByLevel[19] = 80000;
    _cumulativeXpByLevel[20] = 88000;
    _loaded = true;
    _initialized = true;
  }

  /// <summary>Resolves a mod's config.json path via SMAPI internals, with recursive fallback.</summary>
  private static string? GetModConfigPath(IModHelper helper, string modId)
  {
    IModInfo? modInfo = helper.ModRegistry.Get(modId);
    if (modInfo == null)
    {
      return null;
    }

    // SMAPI's IModInfo implementation has a DirectoryPath property (not on the public interface)
    string? dirPath = modInfo.GetType().GetProperty("DirectoryPath")?.GetValue(modInfo)?.ToString();

    if (!string.IsNullOrEmpty(dirPath))
    {
      return Path.Combine(dirPath, "config.json");
    }

    // Fallback: search Mods folder recursively
    string modsDir = Path.GetDirectoryName(helper.DirectoryPath)!;
    foreach (
      string manifestPath in Directory.EnumerateFiles(
        modsDir,
        "manifest.json",
        SearchOption.AllDirectories
      )
    )
    {
      try
      {
        string manifestJson = File.ReadAllText(manifestPath);
        using var doc = JsonDocument.Parse(manifestJson);
        if (
          doc.RootElement.TryGetProperty("UniqueID", out JsonElement idProp)
          && string.Equals(idProp.GetString(), modId, StringComparison.OrdinalIgnoreCase)
        )
        {
          return Path.Combine(Path.GetDirectoryName(manifestPath)!, "config.json");
        }
      }
      catch
      {
        // Skip unreadable manifests
      }
    }

    return null;
  }
}
