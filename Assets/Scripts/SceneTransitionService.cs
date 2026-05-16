using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Persistent service that performs slide-wipe transitions between scenes.
// First Goto() call self-instantiates and survives scene loads.
//
// Usage:
//   SceneTransitionService.Goto("MenuScene");
//   SceneTransitionService.Goto("MainScene");
//
// Pattern (AGENTS §5): the wipe Canvas + RawImage are built procedurally
// here — but they are *transient overlay UI* during a scene transition,
// not gameplay structural UI. They live for ~1s per transition and don't
// belong in either scene.
public class SceneTransitionService : MonoBehaviour
{
    static SceneTransitionService _instance;
    Canvas        _canvas;
    RectTransform _panel;
    Image         _img;

    [SerializeField] float _slideDuration = 0.4f;

    public static void Goto(string sceneName)
    {
        EnsureInstance();
        _instance.StartCoroutine(_instance.TransitionRoutine(sceneName));
    }

    static void EnsureInstance()
    {
        if (_instance != null) return;
        var go = new GameObject("SceneTransitionService");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<SceneTransitionService>();
        _instance.BuildOverlay();
    }

    void BuildOverlay()
    {
        var canvasGO = new GameObject("TransitionCanvas");
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999; // above everything
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        var panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        _panel = panelGO.AddComponent<RectTransform>();
        _panel.anchorMin = Vector2.zero; _panel.anchorMax = Vector2.one;
        _panel.offsetMin = _panel.offsetMax = Vector2.zero;
        _img = panelGO.AddComponent<Image>();
        _img.color = Color.black;
        _img.raycastTarget = true;

        // Start off-screen left
        SetPanelX(-Screen.width);
    }

    void SetPanelX(float x)
    {
        _panel.anchorMin = Vector2.zero; _panel.anchorMax = Vector2.one;
        _panel.anchoredPosition = new Vector2(x, 0);
    }

    IEnumerator TransitionRoutine(string sceneName)
    {
        // Slide in from left → cover screen
        yield return Slide(-Screen.width, 0, _slideDuration);

        // Load scene while opaque
        var op = SceneManager.LoadSceneAsync(sceneName);
        while (op != null && !op.isDone) yield return null;

        // Slide out to right
        yield return Slide(0, Screen.width, _slideDuration);

        // Reset position for next transition
        SetPanelX(-Screen.width);
    }

    IEnumerator Slide(float fromX, float toX, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            // ease-out cubic
            k = 1f - Mathf.Pow(1f - k, 3f);
            SetPanelX(Mathf.Lerp(fromX, toX, k));
            yield return null;
        }
        SetPanelX(toX);
    }
}
