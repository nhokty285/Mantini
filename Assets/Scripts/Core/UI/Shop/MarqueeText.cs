using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Auto-scrolls text right-to-left when content exceeds the visible area.
/// Time Complexity: O(1) per frame update (simple position translate).
/// </summary>
public class MarqueeText : MonoBehaviour
{
    [SerializeField] private float speed = 60f;        // pixels/second
    [SerializeField] private float delayBeforeScroll = 1.2f;
    [SerializeField] private float pauseAtEnd = 0.8f;

    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private RectTransform _rect;
    [SerializeField] private RectTransform _parentRect;
    private float _textWidth;
    private float _containerWidth;
    private Coroutine _scrollCoroutine;

    private void Awake()
    {

    }

    public void StartScroll()
    {
        if (_scrollCoroutine != null) StopCoroutine(_scrollCoroutine);
        _scrollCoroutine = StartCoroutine(ScrollRoutine());
    }

    public void StopScroll()
    {
        if (_scrollCoroutine != null) StopCoroutine(_scrollCoroutine);
        _rect.localPosition = Vector3.zero;
    }

    private IEnumerator ScrollRoutine()
    {
        // Đợi TMP cập nhật layout xong
        yield return new WaitForEndOfFrame();

        _text.ForceMeshUpdate();
        _textWidth = _text.preferredWidth;
        _containerWidth = _parentRect.rect.width;

        // Không scroll nếu text vừa khít
        if (_textWidth <= _containerWidth)
        {
            _rect.localPosition = Vector3.zero;
            yield break;
        }

        float scrollDistance = _textWidth - _containerWidth;

        while (true)
        {
            // Reset về đầu
            _rect.localPosition = Vector3.zero;
            yield return new WaitForSeconds(delayBeforeScroll);

            // Scroll từ phải sang trái
            float elapsed = 0f;
            float duration = scrollDistance / speed;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float x = Mathf.Lerp(0, -scrollDistance, elapsed / duration);
                _rect.localPosition = new Vector3(x, 0, 0);
                yield return null;
            }

            yield return new WaitForSeconds(pauseAtEnd);
        }
    }
}