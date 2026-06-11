using System.Collections;
using UnityEngine;

// Short show/hide animation for a menu panel. Attach next to the panel root
// (gets a CanvasGroup automatically) and pick a style; MainMenuController
// calls Show()/HideThen() instead of raw SetActive so panel changes glide
// instead of popping. Uses unscaled time. Deliberately quick (≤0.2 s).
[RequireComponent(typeof(CanvasGroup))]
public class PanelTransition : MonoBehaviour
{
    public enum Style { Fade, SlideFromRight, SlideFromLeft, Pop }

    [SerializeField] Style _style       = Style.Fade;
    [SerializeField] float _showTime    = 0.18f;
    [SerializeField] float _hideTime    = 0.10f;
    [SerializeField] float _slideOffset = 48f;

    CanvasGroup   _cg;
    RectTransform _rt;
    Vector2       _homePos;
    bool          _homeCaptured;
    Coroutine     _routine;

    // Lazy init — Show() is called on panels that start inactive, where Awake
    // has not run yet.
    void EnsureInit()
    {
        if (_cg == null) _cg = GetComponent<CanvasGroup>();
        if (_rt == null) _rt = (RectTransform)transform;
        if (!_homeCaptured)
        {
            _homePos      = _rt.anchoredPosition;
            _homeCaptured = true;
        }
    }

    public void Show()
    {
        EnsureInit();
        gameObject.SetActive(true);
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Animate(show: true));
    }

    public void Hide()
    {
        if (!gameObject.activeSelf) return;
        EnsureInit();
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Animate(show: false));
    }

    IEnumerator Animate(bool show)
    {
        float dur = show ? _showTime : _hideTime;
        Vector2 slid = _homePos + SlideDir() * _slideOffset;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            if (!show) k = 1f - k;
            float e = 1f - (1f - k) * (1f - k);   // ease-out
            _cg.alpha = e;
            switch (_style)
            {
                case Style.SlideFromRight:
                case Style.SlideFromLeft:
                    _rt.anchoredPosition = Vector2.Lerp(slid, _homePos, e);
                    break;
                case Style.Pop:
                    _rt.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, e);
                    break;
            }
            yield return null;
        }

        // settle exact final state
        _cg.alpha            = show ? 1f : 0f;
        _rt.anchoredPosition = _homePos;
        _rt.localScale       = Vector3.one;
        if (!show) gameObject.SetActive(false);
        _routine = null;
    }

    Vector2 SlideDir() => _style switch
    {
        Style.SlideFromRight => Vector2.right,
        Style.SlideFromLeft  => Vector2.left,
        _                    => Vector2.zero,
    };
}
