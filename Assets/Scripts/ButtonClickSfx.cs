using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Plays the shared UI-click sound when this element is clicked. Attach next to
// any clickable UI (Button or IPointerClickHandler target) and assign the
// AudioProfileSO.
//
// Listens at the EventSystem level (IPointerClickHandler) instead of
// Button.onClick on purpose:
//   • several scripts call onClick.RemoveAllListeners() when rebinding slots,
//     which silently wiped an onClick-based hook;
//   • right-clicks and custom IPointerClickHandler UIs (card grid, deck slots)
//     never fire onClick at all.
// All instances share one lazily-created 2D AudioSource.
public class ButtonClickSfx : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] AudioProfileSO _profile;

    static AudioSource _shared;   // one 2D source for every clickable

    Button _button;               // optional — used only to respect interactable

    void Awake() => _button = GetComponent<Button>();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable) return;
        Play();
    }

    void Play()
    {
        if (_profile == null || _profile.uiClick == null || !_profile.uiClick.HasClips) return;
        var clip = _profile.uiClick.Pick();
        if (clip == null) return;

        if (_shared == null)
        {
            var go = new GameObject("[UIClickAudio]");
            DontDestroyOnLoad(go);
            _shared = go.AddComponent<AudioSource>();
            _shared.playOnAwake  = false;
            _shared.spatialBlend = 0f;
        }
        _shared.pitch = 1f + Random.Range(-_profile.uiClick.pitchJitter, _profile.uiClick.pitchJitter);
        _shared.PlayOneShot(clip, _profile.uiClick.volume * _profile.masterVolume * GameSettings.Sfx);
    }
}
