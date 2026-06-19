using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gắn lên GO_StraightLine (VFX vẽ đường qua 4 điểm A→B→C→D).
/// Hiệu ứng "guide light" kiểu Crystal of Atlan:
///   - Điểm đầu (A) luôn bám chân player real-time.
///   - Đường giữa (B,C) uốn theo NavMesh path, vòng obstacle.
///   - Điểm cuối (D) ghim tại NPC.
///   - 4 điểm di chuyển MƯỢt bằng Vector3.Lerp mỗi frame.
///
/// TÁCH 2 TẦNG (OPTIMIZE.md — không để logic nặng trong Update):
///   - Tầng PATH  : NavMesh.CalculatePath() chạy coroutine mỗi pathUpdateInterval (0.15s).
///   - Tầng VISUAL: Lerp 4 điểm mỗi frame trong Update (O(1), 0 GC).
///
/// Tối ưu:
///   - NPC tĩnh -> snap NavMesh & cache _endPos MỘT LẦN (bỏ 1 SamplePosition mỗi cycle).
///   - Gộp tính độ dài đoạn: điền _segLen[] 1 lần rồi resample B & C dùng lại (3 vòng -> 1 vòng).
///   - Reuse NavMeshPath + buffer corners/segLen + cache WaitForSeconds -> 0 GC mỗi frame.
///
/// => Không sửa TutorialGamePlay.cs: ăn theo OnEnable/OnDisable của SetActive.
/// </summary>
public class StraightLinePathGuide : MonoBehaviour
{
    #region Inspector

    [Header("══════ Targets ══════")]
    [Tooltip("NPC đích (kéo NPC_Eyewear_New vào). Player tự lấy từ PlayerController.Instance.")]
    [SerializeField] private Transform targetNpc;

    [Header("══════ Line Control Points (A→B→C→D) ══════")]
    [Tooltip("4 điểm con của VFX. Để trống sẽ tự tìm theo tên child 'A'/'B'/'C'/'D'.")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField] private Transform pointC;
    [SerializeField] private Transform pointD;

    [Header("══════ Settings ══════")]
    [Tooltip("Tốc độ Lerp visual mỗi frame (lớn = snap nhanh, nhỏ = mượt chậm hơn).")]
    [SerializeField] private float lerpSpeed = 8f;
    [Tooltip("Giây giữa 2 lần recalc NavMesh path (0.15 = nhẹ, đủ mượt). Tách khỏi Update.")]
    [SerializeField] private float pathUpdateInterval = 0.15f;
    [Tooltip("Bán kính snap về NavMesh gần nhất.")]
    [SerializeField] private float navSampleRadius = 3f;
    [Tooltip("Nâng line lên khỏi mặt đất.")]
    [SerializeField] private float heightOffset = 0.25f;
    [Tooltip("Thời gian tối đa chờ Player spawn xong.")]
    [SerializeField] private float maxWaitForPlayer = 5f;

    #endregion

    #region Private State

    // Target positions (tính từ path) — Update Lerp visual về đây. Unity single-thread nên an toàn.
    private Vector3 _targetA;
    private Vector3 _targetB;
    private Vector3 _targetC;
    private Vector3 _targetD;

    private Vector3 _endPos;        // NPC đã snap NavMesh — cache 1 lần (NPC tĩnh)
    private bool _pathReady;        // true khi đã có path đầu tiên -> Update bắt đầu Lerp

    private NavMeshPath _path;                       // reuse — tránh GC
    private readonly Vector3[] _corners = new Vector3[32];  // reuse buffer
    private readonly float[]   _segLen  = new float[32];    // reuse: độ dài từng đoạn corner

    private WaitForSeconds _pathWait;
    private Coroutine _pathRoutine;
    private Transform _player;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _path     = new NavMeshPath();
        _pathWait = new WaitForSeconds(pathUpdateInterval);
        AutoBindPointsIfNeeded();
    }

    private void OnEnable()
    {
        _pathReady = false;
        _pathRoutine = StartCoroutine(PathLoop());
    }

    private void OnDisable()
    {
        if (_pathRoutine != null)
        {
            StopCoroutine(_pathRoutine);
            _pathRoutine = null;
        }
        _pathReady = false;
    }

    /// <summary>VISUAL LAYER — mỗi frame, CHỈ Lerp (O(1), 0 GC).</summary>
    private void Update()
    {
        if (!_pathReady || pointA == null || pointB == null || pointC == null || pointD == null)
            return;

        float t = lerpSpeed * Time.deltaTime;
        pointA.position = Vector3.Lerp(pointA.position, _targetA, t);
        pointB.position = Vector3.Lerp(pointB.position, _targetB, t);
        pointC.position = Vector3.Lerp(pointC.position, _targetC, t);
        pointD.position = Vector3.Lerp(pointD.position, _targetD, t);
    }

    #endregion

    #region Path Layer (Coroutine)

    private IEnumerator PathLoop()
    {
        // Chờ Player spawn xong.
        float elapsed = 0f;
        while (PlayerController.Instance == null)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= maxWaitForPlayer)
            {
                Debug.LogWarning("[StraightLinePathGuide] Hết thời gian chờ Player.");
                yield break;
            }
            yield return null;
        }
        _player = PlayerController.Instance.transform;

        if (targetNpc == null)
        {
            Debug.LogWarning("[StraightLinePathGuide] Chưa gán targetNpc.");
            yield break;
        }

        // Tối ưu: NPC tĩnh -> snap NavMesh & cache 1 lần (D không đổi).
        Vector3 up = Vector3.up * heightOffset;
        _endPos  = TrySampleOnNavMesh(targetNpc.position, out Vector3 npcSnap) ? npcSnap : targetNpc.position;
        _targetD = _endPos + up;

        // Recalc path theo interval.
        while (true)
        {
            RecalcPath();
            yield return _pathWait;
        }
    }

    private void RecalcPath()
    {
        if (_player == null) return;

        Vector3 up = Vector3.up * heightOffset;
        Vector3 startPos = TrySampleOnNavMesh(_player.position, out Vector3 pSnap) ? pSnap : _player.position;

        // A luôn = vị trí player hiện tại (Update sẽ Lerp mượt về đây).
        _targetA = startPos + up;

        _path.ClearCorners();
        bool ok = NavMesh.CalculatePath(startPos, _endPos, NavMesh.AllAreas, _path);

        int count = (ok && _path.status != NavMeshPathStatus.PathInvalid)
                    ? _path.GetCornersNonAlloc(_corners) : 0;

        if (count < 2)
        {
            // Fallback: line thẳng start->end.
            _targetB = Vector3.Lerp(startPos, _endPos, 0.3333f) + up;
            _targetC = Vector3.Lerp(startPos, _endPos, 0.6667f) + up;
            _pathReady = true;
            return;
        }

        // Tối ưu: điền độ dài từng đoạn 1 lần, rồi resample B & C dùng lại (không tính Distance lặp).
        float total = FillSegmentLengths(count);
        _targetB = SampleByLength(count, total, 1f / 3f) + up;
        _targetC = SampleByLength(count, total, 2f / 3f) + up;

        _pathReady = true;
    }

    #endregion

    #region Helpers

    /// <summary>Điền _segLen[i] = khoảng cách corner[i]->corner[i+1], trả tổng. O(count), 1 lần/recalc.</summary>
    private float FillSegmentLengths(int count)
    {
        float total = 0f;
        for (int i = 0; i < count - 1; i++)
        {
            float d = Vector3.Distance(_corners[i], _corners[i + 1]);
            _segLen[i] = d;
            total += d;
        }
        return total;
    }

    /// <summary>Lấy điểm tại tỉ lệ t (0..1) theo chiều dài, dùng lại _segLen đã tính (không recompute Distance).</summary>
    private Vector3 SampleByLength(int count, float total, float t)
    {
        if (total <= 0.0001f) return _corners[0];
        float target = total * Mathf.Clamp01(t);
        float acc = 0f;
        for (int i = 0; i < count - 1; i++)
        {
            float seg = _segLen[i];
            if (acc + seg >= target)
            {
                float u = seg > 0.0001f ? (target - acc) / seg : 0f;
                return Vector3.Lerp(_corners[i], _corners[i + 1], u);
            }
            acc += seg;
        }
        return _corners[count - 1];
    }

    private bool TrySampleOnNavMesh(Vector3 pos, out Vector3 result)
    {
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }
        result = pos;
        return false;
    }

    private void AutoBindPointsIfNeeded()
    {
        if (pointA != null && pointB != null && pointC != null && pointD != null) return;
        foreach (Transform ch in transform)
        {
            switch (ch.name)
            {
                case "A": if (pointA == null) pointA = ch; break;
                case "B": if (pointB == null) pointB = ch; break;
                case "C": if (pointC == null) pointC = ch; break;
                case "D": if (pointD == null) pointD = ch; break;
            }
        }
        if (pointA == null || pointB == null || pointC == null || pointD == null)
            Debug.LogWarning("[StraightLinePathGuide] Thiếu điểm A/B/C/D — kiểm tra lại child GO_StraightLine.");
    }

    #endregion
}
