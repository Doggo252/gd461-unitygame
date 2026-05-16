using UnityEngine;

[CreateAssetMenu(fileName = "HealthBarService", menuName = "Tank Royale/UI/Health Bar Service")]
public class HealthBarServiceSO : ScriptableObject
{
    RectTransform _host;

    public void RegisterCanvas(RectTransform host)   { _host = host; }
    public void UnregisterCanvas(RectTransform host) { if (_host == host) _host = null; }

    public GameObject Spawn(GameObject prefab)
    {
        if (_host == null || prefab == null) return null;
        return Object.Instantiate(prefab, _host);
    }

    public void Despawn(GameObject instance)
    {
        if (instance != null) Object.Destroy(instance);
    }
}
