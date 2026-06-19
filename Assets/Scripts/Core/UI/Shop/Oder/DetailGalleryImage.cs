using System.Threading;
using UnityEngine;
using UnityEngine.UI;
// ─────────────────────────────────────────────────────────────────────────────
// Component nhỏ gắn vào mỗi gallery page để quản lý sprite của riêng nó
// ─────────────────────────────────────────────────────────────────────────────
public class DetailGalleryImage : MonoBehaviour
{
    public Image targetImage;
    private bool _ownsSprite;
    private CancellationTokenSource _cts;

    /// <summary>
    /// Request download ảnh qua ImageDownloadManager.
    /// Callback bị bỏ qua nếu CTS đã cancel hoặc object đã destroy.
    /// </summary>
    public void LoadImage(string url)
    {
        Cancel();
        if (string.IsNullOrEmpty(url) || targetImage == null) return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        ImageDownloadManager.Instance.DownloadImage(
            url,
            texture =>
            {
                if (token.IsCancellationRequested) return;
                if (targetImage == null || !targetImage.gameObject.activeInHierarchy) return;
                ApplyTexture(texture);
            },
            error => GameLog.Warn($"[GalleryImage] Failed: {url} | {error}")
        );
    }

    public void Cancel()
    {
        if (_cts == null) return;
        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
    }

    /// <summary>Destroy sprite local; KHÔNG destroy texture (thuộc CacheService).</summary>
    public void ReleaseSprite()
    {
        Cancel();
        if (_ownsSprite && targetImage != null && targetImage.sprite != null)
        {
            Destroy(targetImage.sprite);
            targetImage.sprite = null;
        }
        _ownsSprite = false;
    }

    private void ApplyTexture(Texture2D texture)
    {
        if (texture == null || targetImage == null) return;

        if (_ownsSprite && targetImage.sprite != null)
        {
            Destroy(targetImage.sprite);
            targetImage.sprite = null;
        }

        targetImage.sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );
        targetImage.preserveAspect = true;
        _ownsSprite = true;
    }

    private void OnDestroy() => ReleaseSprite();
}