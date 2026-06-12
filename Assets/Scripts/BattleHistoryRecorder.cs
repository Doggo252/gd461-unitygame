using System;
using System.Collections.Generic;
using UnityEngine;

// Writes one BattleHistoryService record when the match ends. Counts kills per
// side from the shared KillEventSO over the match, reads the chosen faction
// from DeckConfigSO, and stamps the result on MatchEndEventSO. Place one on a
// scene object in MainScene and wire the three channels.
public class BattleHistoryRecorder : MonoBehaviour
{
    [SerializeField] MatchEndEventSO _matchEndEvent;
    [SerializeField] KillEventSO     _killEvent;
    [SerializeField] DeckConfigSO    _deckConfig;
    [SerializeField] int             _playerTeam = 0;

    int  _allyKills;
    int  _enemyKills;
    bool _recorded;
    readonly List<BattleHistoryService.KillEntry> _kills = new();

    void OnEnable()
    {
        if (_killEvent     != null) _killEvent.OnRaised     += OnKill;
        if (_matchEndEvent != null) _matchEndEvent.OnRaised += OnMatchEnd;
    }

    void OnDisable()
    {
        if (_killEvent     != null) _killEvent.OnRaised     -= OnKill;
        if (_matchEndEvent != null) _matchEndEvent.OnRaised -= OnMatchEnd;
    }

    void OnKill(KillInfo info)
    {
        // A victim on the enemy team is a kill FOR the player, and vice-versa.
        if (info.team == 1 - _playerTeam) _allyKills++;
        else if (info.team == _playerTeam) _enemyKills++;

        // Record the full log line so the records screen can replay this battle.
        // Normalise teams to the player's POV: 0 = ally, 1 = enemy.
        _kills.Add(new BattleHistoryService.KillEntry
        {
            killerName = info.killerName,
            killerTeam = info.killerTeam < 0 ? -1 : (info.killerTeam == _playerTeam ? 0 : 1),
            victimName = info.unitName,
            victimTeam = info.team == _playerTeam ? 0 : 1,
        });
    }

    void OnMatchEnd(MatchEndInfo info)
    {
        if (_recorded) return;
        _recorded = true;

        var outcome = info.winnerTeam < 0           ? BattleHistoryService.Outcome.Draw
                    : info.winnerTeam == _playerTeam ? BattleHistoryService.Outcome.Victory
                                                     : BattleHistoryService.Outcome.Defeat;
        string faction = _deckConfig != null ? _deckConfig.chosenFaction.ToString() : "";

        BattleHistoryService.Record(outcome, info.reason, faction,
                                    _allyKills, _enemyKills, DateTime.UtcNow.Ticks,
                                    new List<BattleHistoryService.KillEntry>(_kills));
    }
}
