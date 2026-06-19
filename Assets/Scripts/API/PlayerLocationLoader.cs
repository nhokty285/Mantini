using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lưu/khôi phục vị trí player qua API. Hot path: SaveCheckLoop chạy mỗi giây
/// nhưng debounce theo distance + time interval (15-30s) để không spam server.
///
/// ⚠️ NOTE: Class name <c>PlayerLocationLoaderFullUrl</c> không khớp filename
/// <c>PlayerLocationLoader.cs</c>. Không đổi vì serialize ref trong scene/prefab
/// có thể đang bind theo tên class — đổi sẽ phá wire.
/// </summary>
public class PlayerLocationLoaderFullUrl : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform; // Để dùng cho debounce save

    [Header("Endpoint (full URL)")]
    [SerializeField]
    private string lastLocationUrl =
        "https://data.mantini-game.c1.hubcom.tech/api/v1/game/player/me/last-location";

    [Header("Debounce + Threshold")]
    [SerializeField] private float timeMinSeconds = 15f;
    [SerializeField] private float timeMaxSeconds = 30f;
    [SerializeField] private float distanceThresholdMeters = 4f;
    [SerializeField] private bool  horizontalOnly = false;

    // Refactor: private fields theo Mantini convention _camelCase
    private bool _isPositionLoadedFromServer = false;
    private float _lastPutTime = -9999f;
    private float _currentInterval;
    private Vector3 _lastSavedPos;
    private bool _isSaving = false;

    [Serializable]
    private class LastLocationAPI
    {
        public float x_position, y_position, z_position;
        public string map_id;

        public Vector3 ToVector3() => new Vector3(x_position, y_position, z_position);
        public static LastLocationAPI FromVector3(Vector3 v, string map) =>
            new LastLocationAPI { x_position = v.x, y_position = v.y, z_position = v.z, map_id = map };
    }

    [Serializable]
    private class LastLocationPayload
    {
        public LastLocationAPI last_location;
    }

    void Awake()
    {
        _currentInterval = UnityEngine.Random.Range(timeMinSeconds, timeMaxSeconds);
        if (playerTransform != null) LoadAndApplyPosition(playerTransform);
        StartCoroutine(SaveCheckLoop());
    }

    private IEnumerator SaveCheckLoop()
    {
        // Cache WaitForSeconds 1 lần — zero alloc per loop iteration
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            yield return wait;
            if (playerTransform == null || !_isPositionLoadedFromServer || _isSaving) continue;

            float dist = DistanceFromLastSaved(playerTransform.position);
            if (dist >= distanceThresholdMeters || Time.time - _lastPutTime >= _currentInterval)
                SaveCurrentLocation(SceneManager.GetActiveScene().name);
        }
    }

    /// <summary>
    /// Public method để GameplayPlayerSpawner gọi: GET vị trí từ backend và set cho target transform.
    /// </summary>
    public void LoadAndApplyPosition(Transform target)
    {
        if (target == null)
        {
            Debug.LogError("[PlayerLocationLoader] Target transform is null!");
            return;
        }

        // Gán playerTransform để debounce save hoạt động sau này
        playerTransform = target;

        APIClient.Instance.GetFull(lastLocationUrl,
            onSuccess: (json) =>
            {
                var loc = JsonUtility.FromJson<LastLocationAPI>(json);

                // DISABLE PHYSICS trước khi set vị trí (tránh nudge / collision)
                var rb = target.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

                target.position = loc.ToVector3();
                _lastSavedPos = target.position;
                _lastPutTime = Time.time;

                // ENABLE lại physics + mark loaded
                if (rb != null)
                    StartCoroutine(EnablePhysicsNextFrame(rb));

                _isPositionLoadedFromServer = true; // cho phép save từ giờ

#if UNITY_EDITOR
                GameLog.Info($"[PlayerLocationLoader] Loaded position from server: {target.position} (map={loc.map_id})");
#endif
            },
            onError: (err) =>
            {
                GameLog.Warn($"[PlayerLocationLoader] Failed to load position, keeping spawn default.\n{err}");
                _lastSavedPos = target.position;
                _lastPutTime = Time.time;
                _isPositionLoadedFromServer = true;
            }
        );
    }

    private IEnumerator EnablePhysicsNextFrame(Rigidbody rb)
    {
        yield return null;
        if (rb != null) rb.isKinematic = false;
    }

    private float DistanceFromLastSaved(Vector3 current)
    {
        if (horizontalOnly)
        {
            // Bỏ qua trục Y — tránh chênh lệch cao độ làm save không cần thiết
            Vector2 a = new Vector2(current.x, current.z);
            Vector2 b = new Vector2(_lastSavedPos.x, _lastSavedPos.z);
            return Vector2.Distance(a, b);
        }
        return Vector3.Distance(current, _lastSavedPos);
    }

    public void SaveCurrentLocation(string mapId)
    {
        if (playerTransform == null) return;

        var p = playerTransform.position;
        if (!IsValid(p))
        {
            Debug.LogError("[PlayerLocationLoader] Invalid position, abort PUT");
            return;
        }

        var payload = new LastLocationPayload
        {
            last_location = LastLocationAPI.FromVector3(p, mapId)
        };

        string json = JsonUtility.ToJson(payload);
        _isSaving = true;

        APIClient.Instance.PutJsonFull(lastLocationUrl, json,
            onSuccess: (res) =>
            {
                _lastSavedPos = p;
                _lastPutTime = Time.time;
                _currentInterval = UnityEngine.Random.Range(timeMinSeconds, timeMaxSeconds);
                _isSaving = false;
#if UNITY_EDITOR
                GameLog.Info($"[PlayerLocationLoader] Saved position: {p}, scene={mapId}, nextInterval={_currentInterval:0.1}s");
#endif
            },
            onError: (err) =>
            {
                _isSaving = false;
                Debug.LogError($"[PlayerLocationLoader] Save failed: {err}");
            });
    }

    private static bool IsValid(Vector3 v)
        => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
             float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

    void OnApplicationPause(bool pause)
    {
        if (pause && !_isSaving && playerTransform != null)
            SaveCurrentLocation(SceneManager.GetActiveScene().name);
    }

    void OnApplicationQuit()
    {
        if (!_isSaving && playerTransform != null)
            SaveCurrentLocation(SceneManager.GetActiveScene().name);
    }
}