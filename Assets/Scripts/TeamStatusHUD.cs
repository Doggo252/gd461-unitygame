using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Drives the two top-corner status readouts:
//   left  = ALLY:  L-FOB ■  R-FOB ■  HQ ■   +  live kill count
//   right = ENEMY: mirrored
// Squares are green while the structure stands and turn red when it is
// destroyed (rich-text colours). Kill counts tick on every KillEventSO raise —
// a victim on team 1 is a kill for the player, a victim on team 0 one for the AI.
public class TeamStatusHUD : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] UnitRegistrySO _registry;
    [SerializeField] KillEventSO    _killEvent;
    [SerializeField] int            _playerTeam = 0;

    [Header("UI — authored in scene (TopStrip corner texts)")]
    [SerializeField] Text _allyText;    // top-left
    [SerializeField] Text _enemyText;   // top-right

    const string Alive = "#37D24A";
    const string Dead  = "#E63C2E";

    int _allyKills;   // tanks the player's team destroyed
    int _enemyKills;  // tanks the AI destroyed

    readonly Dictionary<ObjectiveTarget, Action> _deathHandlers = new();

    void OnEnable()
    {
        if (_killEvent != null) _killEvent.OnRaised += OnKill;
    }

    void OnDisable()
    {
        if (_killEvent != null) _killEvent.OnRaised -= OnKill;
        foreach (var kv in _deathHandlers)
            if (kv.Key != null && kv.Key.Health != null) kv.Key.Health.OnDeath -= kv.Value;
        _deathHandlers.Clear();
    }

    // Objectives register in their own OnEnable — subscribe after, in Start.
    void Start()
    {
        if (_registry != null)
        {
            foreach (var o in _registry.Objectives)
            {
                if (o == null || o.Health == null || _deathHandlers.ContainsKey(o)) continue;
                var obj = o;
                Action death = () => Refresh();
                obj.Health.OnDeath += death;
                _deathHandlers[obj] = death;
            }
        }
        Refresh();
    }

    void OnKill(KillInfo info)
    {
        if (info.team == 1 - _playerTeam) _allyKills++;
        else if (info.team == _playerTeam) _enemyKills++;
        Refresh();
    }

    void Refresh()
    {
        if (_allyText  != null) _allyText.text  = Build(_playerTeam,     _allyKills,  leftAligned: true);
        if (_enemyText != null) _enemyText.text = Build(1 - _playerTeam, _enemyKills, leftAligned: false);
    }

    string Build(int team, int kills, bool leftAligned)
    {
        string l = Square(team, "Left");
        string r = Square(team, "Right");
        string h = Square(team, "HQ");
        string status = $"L-FOB {l}   R-FOB {r}   HQ {h}";
        string killsStr = $"KILLS <b>{kills}</b>";
        return leftAligned ? $"{status}      {killsStr}" : $"{killsStr}      {status}";
    }

    string Square(int team, string key)
    {
        bool alive = true;
        if (_registry != null)
        {
            foreach (var o in _registry.Objectives)
            {
                if (o == null || o.Team != team) continue;
                bool match = key == "HQ" ? o.name.Contains("HQ") : o.name.Contains(key);
                if (match) { alive = o.IsAlive; break; }
            }
        }
        return $"<color={(alive ? Alive : Dead)}>■</color>";
    }
}
