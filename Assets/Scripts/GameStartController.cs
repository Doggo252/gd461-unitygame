using UnityEngine;
using UnityEngine.UI;

// Holds the game at Time.timeScale = 0 until the player deploys their first unit.
// Shows a "DEPLOY A UNIT TO BEGIN" prompt on the HUD.
//
// Detects the first player deploy via CpChangedEventSO (P1): when the player's CP
// decreases for the first time it means they spent CP = deployed a unit.
// Everything still works at timeScale = 0 because UI input, InputSystem callbacks,
// NavMesh queries, and Instantiate are all frame-based, not time-based.
public class GameStartController : MonoBehaviour
{
    [Header("Event — detect first player deploy")]
    [SerializeField] CpChangedEventSO _playerCpChangedEvent;

    [Header("UI — wire in scene")]
    [SerializeField] GameObject _startPromptRoot;   // panel to show before first deploy
    [SerializeField] Text       _promptText;         // "DROP A UNIT TO BEGIN"

    bool _started;
    int  _lastCp = -1;

    void Awake()
    {
        // Pause the game immediately — nothing moves until the player deploys
        Time.timeScale = 0f;
        if (_startPromptRoot != null) _startPromptRoot.SetActive(true);
    }

    void OnEnable()
    {
        if (_playerCpChangedEvent != null)
            _playerCpChangedEvent.OnRaised += OnCpChanged;
    }

    void OnDisable()
    {
        if (_playerCpChangedEvent != null)
            _playerCpChangedEvent.OnRaised -= OnCpChanged;
    }

    void OnCpChanged(CpChangedInfo info)
    {
        if (_started) return;

        // On first event, just record the starting CP — this fires when the
        // CommandPointsManager initialises with startingCp = 5.
        if (_lastCp < 0)
        {
            _lastCp = info.currentCp;
            return;
        }

        // CP went DOWN → player spent CP = deployed a unit → start the game
        if (info.currentCp < _lastCp)
            StartGame();

        _lastCp = info.currentCp;
    }

    void StartGame()
    {
        if (_started) return;
        _started = true;
        Time.timeScale = 1f;
        if (_startPromptRoot != null) _startPromptRoot.SetActive(false);
    }
}
