using UnityEngine;
using UnityEngine.UI;

// Data binding for the floating tank name label prefab.
// Pair with WorldHealthBarTracker._nameLabelPrefab.
public class NameLabelBinding : MonoBehaviour
{
    public RectTransform root;
    public Text          nameText;
}
