using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FloatingJoystick : Joystick
{
    private const byte ALPHA_ACTIVE = 255;
    private const byte ALPHA_INACTIVE = 128;

    private Graphic[] _bgGraphics;

    protected override void Start()
    {
        base.Start();
        _bgGraphics = background.GetComponentsInChildren<Graphic>(includeInactive: true);
        background.gameObject.SetActive(true);
        SetBackgroundAlpha(ALPHA_INACTIVE);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        // BƯỚC 1: Gọi base trước → OnDrag chạy → cam được set đúng
        //         background.position được Unity biết
        base.OnPointerDown(eventData);

        // BƯỚC 2: SAU KHI base chạy xong, mới reposition background
        background.anchoredPosition = ScreenPointToAnchoredPosition(eventData.position);

        // BƯỚC 3: Force canvas rebuild để anchor của handle sync ngay
        Canvas.ForceUpdateCanvases();

        // BƯỚC 4: Reset handle về tâm background (sau khi anchor đã đúng)
        handle.anchoredPosition = Vector2.zero;

        SetBackgroundAlpha(ALPHA_ACTIVE);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        SetBackgroundAlpha(ALPHA_INACTIVE);
        base.OnPointerUp(eventData);
    }

    private void SetBackgroundAlpha(byte alpha)
    {
        if (_bgGraphics == null) return;
        float normalized = alpha / 255f;
        for (int i = 0; i < _bgGraphics.Length; i++)
        {
            if (_bgGraphics[i] == null) continue;
            Color c = _bgGraphics[i].color;
            c.a = normalized;
            _bgGraphics[i].color = c;
        }
    }
}