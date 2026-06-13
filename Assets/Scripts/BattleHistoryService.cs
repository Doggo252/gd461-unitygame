using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Persistent record of every completed match, backed by PlayerPrefs (survives
// restarts). Static so any scene can read it. Written on match end by
// BattleHistoryRecorder; read by the menu's BattleHistoryPanel.
//
// IL2CPP/build note: the per-match kill log is stored as a single flat STRING
// (`killLog`), NOT a nested List<KillEntry>. JsonUtility round-trips a nested
// generic list inconsistently under IL2CPP/managed-stripping (it came back empty
// in Android builds while working in the Mono editor — the "battle log empty in
// build" bug). A plain string field serialises identically everywhere.
// The victory-screen log additionally reads `LastMatchKills` straight from
// memory, so it never depends on the PlayerPrefs/JSON round-trip at all.
public static class BattleHistoryService
{
    const string KEY      = "TankRoyale.BattleHistory";
    const int    MAX_KEPT = 60;

    // Flat-blob delimiters. Tank/structure names never contain tabs or newlines,
    // and Clean() strips them defensively; JsonUtility stores them verbatim.
    const char FieldSep = '\t';   // between a kill's fields
    const char RecSep   = '\n';   // between kills

    public enum Outcome { Victory, Defeat, Draw }

    // One line of the per-match battle log (a single kill). In-memory / API type
    // only — never serialised by JsonUtility (see killLog encoding below).
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
        public long   utcTicks;    // DateTime.UtcNow.Ticks at match end
        public string killLog;     // flat-encoded battle log (see EncodeKills) — build-safe
    }

    [Serializable]
    class Wrapper { public List<BattleRecord> records = new(); }

    // The just-finished match's kill log, kept in memory by Record() so the
    // victory screen can show it without any serialization round-trip.
    static readonly List<KillEntry> _lastMatchKills = new();
    public static IReadOnlyList<KillEntry> LastMatchKills => _lastMatchKills;

    public static IReadOnlyList<BattleRecord> All => Load().records;

    public static void Record(Outcome outcome, string reason, string faction,
                              int allyKills, int enemyKills, long utcTicks,
                              List<KillEntry> kills = null)
    {
        // Remember in memory for the victory-screen battle log (build-safe path).
        _lastMatchKills.Clear();
        if (kills != null) _lastMatchKills.AddRange(kills);

        var w = Load();
        w.records.Insert(0, new BattleRecord
        {
            outcome    = (int)outcome,
            reason     = reason ?? "",
            faction    = faction ?? "",
            allyKills  = allyKills,
            enemyKills = enemyKills,
            utcTicks   = utcTicks,
            killLog    = EncodeKills(kills),
        });
        if (w.records.Count > MAX_KEPT) w.records.RemoveRange(MAX_KEPT, w.records.Count - MAX_KEPT);
        PlayerPrefs.SetString(KEY, JsonUtility.ToJson(w));
        PlayerPrefs.Save();
    }

    public static void Clear()
    {
        _lastMatchKills.Clear();
        PlayerPrefs.DeleteKey(KEY);
        PlayerPrefs.Save();
    }

    // Decode a stored record's flat kill log into entries (for the records detail view).
    public static List<KillEntry> KillsOf(BattleRecord r) => DecodeKills(r.killLog);

    // ── Flat-string codec (IL2CPP-safe; no nested generics for JsonUtility) ─────

    static string EncodeKills(List<KillEntry> kills)
    {
        if (kills == null || kills.Count == 0) return "";
        var sb = new StringBuilder();
        for (int i = 0; i < kills.Count; i++)
        {
            if (i > 0) sb.Append(RecSep);
            var k = kills[i];
            sb.Append(Clean(k.killerName)).Append(FieldSep).Append(k.killerTeam).Append(FieldSep)
              .Append(Clean(k.victimName)).Append(FieldSep).Append(k.victimTeam);
        }
        return sb.ToString();
    }

    static List<KillEntry> DecodeKills(string blob)
    {
        var list = new List<KillEntry>();
        if (string.IsNullOrEmpty(blob)) return list;
        foreach (var rec in blob.Split(RecSep))
        {
            var p = rec.Split(FieldSep);
            if (p.Length < 4) continue;
            int.TryParse(p[1], out int kt);
            int.TryParse(p[3], out int vt);
            list.Add(new KillEntry { killerName = p[0], killerTeam = kt, victimName = p[2], victimTeam = vt });
        }
        return list;
    }

    static string Clean(string s) =>
        string.IsNullOrEmpty(s) ? "" : s.Replace(FieldSep, ' ').Replace(RecSep, ' ');

    // ── Shared row formatter (victory log + records detail read identically) ────

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

    static Wrapper Load()
    {
        string json = PlayerPrefs.GetString(KEY, "");
        if (string.IsNullOrEmpty(json)) return new Wrapper();
        try { return JsonUtility.FromJson<Wrapper>(json) ?? new Wrapper(); }
        catch { return new Wrapper(); }
    }
}
