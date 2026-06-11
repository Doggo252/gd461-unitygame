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

    [Serializable]
    public struct BattleRecord
    {
        public int    outcome;     // BattleHistoryService.Outcome
        public string reason;      // "HQDestroyed" | "Timer" | "Casualties" | ...
        public string faction;     // "Allies" | "Axis"
        public int    allyKills;
        public int    enemyKills;
        public long   utcTicks;     // DateTime.UtcNow.Ticks at match end
    }

    [Serializable]
    class Wrapper { public List<BattleRecord> records = new(); }

    public static IReadOnlyList<BattleRecord> All => Load().records;

    public static void Record(Outcome outcome, string reason, string faction,
                              int allyKills, int enemyKills, long utcTicks)
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
        });
        if (w.records.Count > MAX_KEPT) w.records.RemoveRange(MAX_KEPT, w.records.Count - MAX_KEPT);
        PlayerPrefs.SetString(KEY, JsonUtility.ToJson(w));
        PlayerPrefs.Save();
    }

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
