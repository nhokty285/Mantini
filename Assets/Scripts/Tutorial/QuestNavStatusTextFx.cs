using TMPro;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Animation DOTween cho Text_QuestNav_StatusText ("Đang tự động tìm đường...").
/// Gắn TRỰC TIẾP lên GameObject Text_QuestNav_StatusText — không cần sửa QuestNavigator.cs.
///
/// Cơ chế: QuestNavigator chỉ SetActive(true/false) lên statusTextObject (object cha "Image").
/// Khi cha active → Unity tự gọi OnEnable() trên component con này → animation tự chạy.
/// Khi cha inactive → OnDisable() tự gọi → DOKill animation, không leak tween.
///
/// Animation: pulse scale nhẹ (loop yoyo) + fade alpha nhịp nhàng, tạo cảm giác "đang xử lý".
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class QuestNavStatusTextFx : MonoBehaviour
{
    [Header("══════ Pulse Scale ══════")]
    [SerializeField] private float pulseScaleMin = 0.96f;
    [SerializeField] private float pulseScaleMax = 1.06f;
    [SerializeField] private float pulseDuration = 0.55f;

    [Header("══════ Fade Alpha ══════")]
    [SerializeField] private float fadeAlphaMin = 0.55f;
    [SerializeField] private float fadeAlphaMax = 1f;
    [SerializeField] private float fadeDuration = 0.7f;

    private TextMeshProUGUI _text;
    private Sequence _sequence;

    private void Awake()
    {
        _text = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        PlayLoop();
    }

    private void OnDisable()
    {
        // Kill tween + reset trạng thái — tránh leak tween và tránh scale/alpha
        // bị kẹt giữa chừng khi statusTextObject bị ẩn đột ngột (joystick interrupt, timeout...).
        _sequence?.Kill();
        _sequence = null;
        transform.localScale = Vector3.one;
        if (_text != null)
        {
            var c = _text.color;
            c.a = fadeAlphaMax;
            _text.color = c;
        }
    }

    private void PlayLoop()
    {
        _sequence?.Kill();
        transform.localScale = Vector3.one * pulseScaleMin;

        if (_text != null)
        {
            var c = _text.color;
            c.a = fadeAlphaMin;
            _text.color = c;
        }

        _sequence = DOTween.Sequence();
        _sequence.Append(transform.DOScale(pulseScaleMax, pulseDuration).SetEase(Ease.InOutSine));
        _sequence.Join(_text != null
            ? _text.DOFade(fadeAlphaMax, fadeDuration).SetEase(Ease.InOutSine)
            : DOTween.Sequence());
        _sequence.SetLoops(-1, LoopType.Yoyo);
        _sequence.SetTarget(this); // tránh xung đột nếu code khác lỡ DOKill(transform)
        _sequence.SetUpdate(true); // chạy cả khi Time.timeScale = 0 (pause), vì đây là UI loading indicator
    }
}
