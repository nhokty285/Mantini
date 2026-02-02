using System;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    private bool isPositionLoadedFromServer = false;
    private float lastPutTime = -9999f;
    private float currentInterval;
    private Vector3 lastSavedPos;
    private bool isSaving = false;

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
        currentInterval = UnityEngine.Random.Range(timeMinSeconds, timeMaxSeconds);

        // Nếu playerTransform được gán trong Inspector, tự động load vị trí cho nó
        if (playerTransform != null)
        {
            LoadAndApplyPosition(playerTransform);
        }
    }

    void FixedUpdate()
    {
        if (playerTransform == null) return;

        if (!isPositionLoadedFromServer) return;

        float dist = DistanceFromLastSaved(playerTransform.position);
        float elapsed = Time.time - lastPutTime;

        if (!isSaving && (dist >= distanceThresholdMeters || elapsed >= currentInterval))
        {
            string sceneName = SceneManager.GetActiveScene().name;
            SaveCurrentLocation(sceneName);
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
                //DISABLE PHYSICS trước khi set vị trí
                var rb = target.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

                target.position = loc.ToVector3();
                lastSavedPos = target.position;
                lastPutTime = Time.time;

                // ✅ FIX #3: ENABLE lại physics + mark loaded
                if (rb != null)
                    StartCoroutine(EnablePhysicsNextFrame(rb));

                isPositionLoadedFromServer = true;  // ✅ ALLOW SAVE từ giờ

                Debug.Log($"[PlayerLocationLoader] Loaded position from server: {target.position} (map={loc.map_id})");
            },
            onError: (err) =>
            {
                Debug.LogWarning($"[PlayerLocationLoader] Failed to load position, keeping spawn default.\n{err}");
                lastSavedPos = target.position;
                lastPutTime = Time.time;
                isPositionLoadedFromServer = true;
            }
        );


    }
    System.Collections.IEnumerator EnablePhysicsNextFrame(Rigidbody rb)
    {
        yield return null;  // Next frame
        rb.isKinematic = false;
    }

    float DistanceFromLastSaved(Vector3 current)
    {
        if (horizontalOnly)
        {
            Vector2 a = new Vector2(current.x, current.z);
            Vector2 b = new Vector2(lastSavedPos.x, lastSavedPos.z);
            return Vector2.Distance(a, b);
        }
        return Vector3.Distance(current, lastSavedPos);
    }

    public void SaveCurrentLocation(string mapId)
    {
        var p = playerTransform.position;
        if (!IsValid(p)) { Debug.LogError("Invalid position, abort PUT"); return; }

        var payload = new LastLocationPayload
        {
            last_location = LastLocationAPI.FromVector3(p, mapId)
        };

        string json = JsonUtility.ToJson(payload);
        isSaving = true;

        APIClient.Instance.PutJsonFull(lastLocationUrl, json,
            onSuccess: (res) =>
            {
                lastSavedPos = p;
                lastPutTime = Time.time;
                currentInterval = UnityEngine.Random.Range(timeMinSeconds, timeMaxSeconds);
                isSaving = false;
                Debug.Log($"[PlayerLocationLoader] Saved position: {p}, scene={mapId}, nextInterval={currentInterval:0.1}s");
            },
            onError: (err) =>
            {
                isSaving = false;
                Debug.LogError($"[PlayerLocationLoader] Save failed: {err}");
            });
    }

    bool IsValid(Vector3 v)
    {
        return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                 float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }

    void OnApplicationPause(bool pause)
    {
        if (pause && !isSaving && playerTransform != null)
            SaveCurrentLocation(SceneManager.GetActiveScene().name);
    }

    void OnApplicationQuit()
    {
        if (!isSaving && playerTransform != null)
            SaveCurrentLocation(SceneManager.GetActiveScene().name);
    }
}
