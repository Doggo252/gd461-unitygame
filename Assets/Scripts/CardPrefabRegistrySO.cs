using System;
using System.Collections.Generic;
using UnityEngine;

// Maps TankType enum values to their instantiable prefabs.
// Wire all 13 entries in the Inspector on Assets/Data/Cards/CardPrefabRegistry.asset.
// The runtime dictionary is rebuilt from the serialized list in OnEnable (same
// pattern as UnitRegistrySO clearing on domain reload).
[CreateAssetMenu(fileName = "CardPrefabRegistry", menuName = "Tank Royale/Cards/Card Prefab Registry")]
public class CardPrefabRegistrySO : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public TankType   tankType;
        public GameObject prefab;
    }

    [SerializeField] List<Entry> _entries = new();

    readonly Dictionary<TankType, GameObject> _map = new();

    void OnEnable()
    {
        _map.Clear();
        foreach (var e in _entries)
            if (e.prefab != null) _map[e.tankType] = e.prefab;
    }

    public bool TryGetPrefab(TankType type, out GameObject prefab)
        => _map.TryGetValue(type, out prefab);
}
