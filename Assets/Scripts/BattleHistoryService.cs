using System;
using System.Collections.Generic;
using UnityEngine;

// Persistent record of every completed match, backed by PlayerPrefs (survives
// restarts). Static so any scene can read it. One JSON blob holds a capped,
// newest-first list of BattleRecord. Written on match end by
// BattleHistoryRecorder; read by the menu's BattleHistoryPanel.
public static class BattleHistoryService
{
    const string KEY      = "TankRoyale.BattleHistory";
    const int    MAX_KEPT = 60;

    public enum Outcome { Victory, Defeat, Draw }

    // One line of the per-match battle log (a single kill), persisted so the
    // records screen can replay any past battle's log — not just its summary.
    [Serializable]
    public struct KillEntry
    {
        public string killerName;
        public int    killerTeam;   // 0 = ally (blue), 1 = enemy (red), -1 = unknown
        public string victimName;
        public int    victimTeam;
    }

    [Serializable]
    public struct BattleRecord
    {
        public int    outcome;     // BattleHistoryService.Outcome
        public string reason;      // "HQDestroyed" | "Timer" | "Casualties" | ...
        public string faction;     // "Allies" | "Axis"
        public int    allyKills;
        public int    enemyKills;
        public long   utcTicks;     // DateTime.UtcNow.Ticks at match end
        public List<KillEntry> kills;  // full battle log for this match (may be null on legacy records)
    }

    [Serializable]
    class Wrapper { public List<BattleRecord> records = new(); }

    public static IReadOnlyList<BattleRecord> All => Load().records;

    public static void Record(Outcome outcome, string reason, string faction,
                              int allyKills, int enemyKills, long utcTicks,
                              List<KillEntry> kills = null)
    {
        var w = Load();
        w.records.Insert(0, new BattleRecord
        {
            outcome    = (int)outcome,
            reason     = reason ?? "",
            faction    = faction ?? "",
            allyKills  = allyKills,
            enemyKills = enemyKills,
            utcTicks   = utcTicks,
            kills      = kills ?? new List<KillEntry>(),
        });
        if (w.records.Count > MAX_KEPT) w.records.RemoveRange(MAX_KEPT, w.records.Count - MAX_KEPT);
        PlayerPrefs.SetString(KEY, JsonUtility.ToJson(w));
        PlayerPrefs.Save();
    }

    // Shared rich-text formatter for one battle-log row, used by both the
    // victory-screen log and the records detail view so they read identically.
    public static string FormatKillRow(int number, KillEntry k)
    {
        string kc    = TeamHex(k.killerTeam);
        string vc    = TeamHex(k.victimTeam);
        string kName = string.IsNullOrEmpty(k.killerName) ? "Unknown" : k.killerName;
        string vName = string.IsNullOrEmpty(k.victimName) ? "Unknown" : k.victimName;
        return $"<color=#777777>{number,2}.</color>   <color={kc}>{kName}</color> " +
               $"<color=#FFFFFF>▶</color> <color={vc}>{vName}</color>";
    }

    static string TeamHex(int team) =>
        team == 0 ? "#66B2FF" :
        team == 1 ? "#FF6666" :
                    "#AAAAAA";

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(KEY);
        PlayerPrefs.Save();
    }

    static Wrapper Load()
    {
        string json = PlayerPrefs.GetString(KEY, "");
        if (string.IsNullOrEmpty(json)) return new Wrapper();
        try { return JsonUtility.FromJson<Wrapper>(json) ?? new Wrapper(); }
        catch { return new Wrapper(); }
    }
}
