/*using System.Collections.Generic;
using UnityEngine;

public class CameraWallOcclusion : MonoBehaviour
{
    public Transform player;
    public LayerMask wallMask;
    public float sphereRadius = 0.25f;
    public float targetHeightOffset = 1.2f;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    private readonly HashSet<WallFadeTarget> currentHits = new();
    private readonly HashSet<WallFadeTarget> previousHits = new();
    private RaycastHit[] hitBuffer = new RaycastHit[16];

    [Header("Gizmos")]
    public bool showGizmos = true;
    public Color castColor = new Color(0f, 1f, 1f, 0.4f);
    public Color hitColor = Color.red;
    public Color clearColor = Color.green;

    private Vector3 _gizmoOrigin;
    private Vector3 _gizmoTarget;
    private int _gizmoHitCount;
    private RaycastHit[] _gizmoHits = new RaycastHit[16];

    int maxFadeTargets = 3;
    int processed = 0;

    private void LateUpdate()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                Debug.Log($"✅ Tìm thấy Player: {player.name}");
            }
            else
            {
                Debug.LogError("❌ Không tìm thấy Player! " +
                    "\n1. Gán tag 'Player' cho Player GameObject\n" +
                    "2. Hoặc drag Player Transform vào ô Player ở Inspector");
            }
        }
        currentHits.Clear();

        Vector3 origin = transform.position;
        Vector3 target = player.position + Vector3.up * targetHeightOffset;
        Vector3 dir = target - origin;
        float distance = dir.magnitude;

        if (distance <= 0.001f) return;
        dir /= distance;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            sphereRadius,
            dir,
            hitBuffer,
            distance,
            wallMask,
            triggerInteraction
        );

        // Cache for Gizmos
        _gizmoOrigin = origin;
        _gizmoTarget = target;
        _gizmoHitCount = hitCount;
        System.Array.Copy(hitBuffer, _gizmoHits, hitCount);

        for (int i = 0; i < hitCount && processed < maxFadeTargets; i++)
        {
            var hit = hitBuffer[i];
            if (hit.collider == null) continue;

            var wall = hit.collider.GetComponentInParent<WallFadeTarget>();
            if (wall == null) continue;

            wall.Initialize();
            currentHits.Add(wall);
            processed++;
        }

        foreach (var wall in currentHits)
        {
            if (!previousHits.Contains(wall))
            {
                wall.enabled = true;
                wall.SetOccluded(true);
            }
        }

        foreach (var wall in previousHits)
        {
            if (!currentHits.Contains(wall))
            {
                wall.enabled = true;
                wall.SetOccluded(false);
            }
        }

        previousHits.Clear();
        foreach (var wall in currentHits)
            previousHits.Add(wall);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        if (!Application.isPlaying) return;
        if (_gizmoOrigin == Vector3.zero && _gizmoTarget == Vector3.zero) return;

        Vector3 dir = _gizmoTarget - _gizmoOrigin;
        float distance = dir.magnitude;
        if (distance <= 0.001f) return;
        dir /= distance;

        bool hasHit = _gizmoHitCount > 0;

        // Vẽ đường SphereCast
        Gizmos.color = hasHit ? castColor : clearColor;
        Gizmos.DrawLine(_gizmoOrigin, _gizmoTarget);

        // Vẽ sphere ở đầu và cuối
        Gizmos.color = new Color(castColor.r, castColor.g, castColor.b, 0.15f);
        Gizmos.DrawSphere(_gizmoOrigin, sphereRadius);
        Gizmos.DrawSphere(_gizmoTarget, sphereRadius);

        // Vẽ từng hit point
        for (int i = 0; i < _gizmoHitCount; i++)
        {
            var hit = _gizmoHits[i];
            if (hit.collider == null) continue;

            // Sphere tại điểm va chạm
            Gizmos.color = hitColor;
            Gizmos.DrawSphere(hit.point, sphereRadius * 0.5f);

            // Normal tại hit point
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(hit.point, hit.point + hit.normal * 0.4f);

            // Label tên object
            UnityEditor.Handles.color = hitColor;
            UnityEditor.Handles.Label(hit.point + Vector3.up * 0.3f, hit.collider.name);
        }

        // Vẽ target point (đầu player)
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(_gizmoTarget, 0.1f);
    }
#endif
   ////l
}*/

using System.Collections.Generic;
using UnityEngine;

public class CameraWallOcclusion : MonoBehaviour
{
    public GameObject player;
    public LayerMask wallMask;
    public float sphereRadius = 0.25f;
    public float targetHeightOffset = 1.2f;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    // ✅ Throttle: chỉ cast mỗi N giây thay vì mỗi frame
    [SerializeField] private float castInterval = 0.05f; // 20 lần/giây đủ dùng
    private float _nextCastTime;

    private readonly HashSet<WallFadeTarget> _currentHits = new();
    private readonly HashSet<WallFadeTarget> _previousHits = new();
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];



    // ✅ Cache component lookup: tránh GetComponentInParent mỗi frame
    private readonly Dictionary<Collider, WallFadeTarget> _wallCache = new();

    private void Start()
    {
        // ✅ Tìm player 1 lần duy nhất trong Start, không tìm lại mỗi frame
        if (player == null)
        {
           player = PlayerController.Instance.gameObject ?? GameObject.FindGameObjectWithTag("Player");
        }
    }

    private void LateUpdate()
    {
        if (player == null) return;

        // ✅ Throttle: skip frame nếu chưa đến interval
        if (Time.time < _nextCastTime) return;
        _nextCastTime = Time.time + castInterval;

        _currentHits.Clear();

        Vector3 origin = transform.position;
        Vector3 target = player.transform.position + Vector3.up * targetHeightOffset;
        Vector3 dir = target - origin;
        float distance = dir.magnitude;

        if (distance < 0.001f) return;
        dir /= distance;

        int hitCount = Physics.SphereCastNonAlloc(
            origin, sphereRadius, dir,
            _hitBuffer, distance, wallMask, triggerInteraction);

        for (int i = 0; i < hitCount ; i++)
        {
            var hit = _hitBuffer[i];
            if (hit.collider == null) continue;

            // ✅ Cache component lookup thay vì GetComponentInParent mỗi lần
            if (!_wallCache.TryGetValue(hit.collider, out var wall))
            {
                wall = hit.collider.GetComponentInParent<WallFadeTarget>();
                _wallCache[hit.collider] = wall; // null cũng cache được
            }

            if (wall == null) continue;
            wall.Initialize();
            _currentHits.Add(wall);
  
        }

        foreach (var wall in _currentHits)
            if (!_previousHits.Contains(wall)) { wall.enabled = true; wall.SetOccluded(true); }

        foreach (var wall in _previousHits)
            if (!_currentHits.Contains(wall)) { wall.enabled = true; wall.SetOccluded(false); }

        _previousHits.Clear();
        foreach (var wall in _currentHits)
            _previousHits.Add(wall);
    }

    // ✅ Dọn cache khi object bị destroy tránh stale reference
    private void OnDestroy() => _wallCache.Clear();

    [Header("Gizmos")]
    public bool showGizmos = true;
    public Color castColor = new Color(0f, 1f, 1f, 0.4f);
    public Color hitColor = Color.red;
    public Color clearColor = Color.green;

    private Vector3 _gizmoOrigin;
    private Vector3 _gizmoTarget;
    private int _gizmoHitCount;
    private RaycastHit[] _gizmoHits = new RaycastHit[16];

    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        if (!Application.isPlaying) return;
        if (_gizmoOrigin == Vector3.zero && _gizmoTarget == Vector3.zero) return;

        Vector3 dir = _gizmoTarget - _gizmoOrigin;
        float distance = dir.magnitude;
        if (distance <= 0.001f) return;
        dir /= distance;

        bool hasHit = _gizmoHitCount > 0;

        // Vẽ đường SphereCast
        Gizmos.color = hasHit ? castColor : clearColor;
        Gizmos.DrawLine(_gizmoOrigin, _gizmoTarget);

        // Vẽ sphere ở đầu và cuối
        Gizmos.color = new Color(castColor.r, castColor.g, castColor.b, 0.15f);
        Gizmos.DrawSphere(_gizmoOrigin, sphereRadius);
        Gizmos.DrawSphere(_gizmoTarget, sphereRadius);

        // Vẽ từng hit point
        for (int i = 0; i < _gizmoHitCount; i++)
        {
            var hit = _gizmoHits[i];
            if (hit.collider == null) continue;

            // Sphere tại điểm va chạm
            Gizmos.color = hitColor;
            Gizmos.DrawSphere(hit.point, sphereRadius * 0.5f);

            // Normal tại hit point
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(hit.point, hit.point + hit.normal * 0.4f);

            // Label tên object
            UnityEditor.Handles.color = hitColor;
            UnityEditor.Handles.Label(hit.point + Vector3.up * 0.3f, hit.collider.name);
        }

        // Vẽ target point (đầu player)
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(_gizmoTarget, 0.1f);
    }
#endif
}