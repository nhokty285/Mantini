using Unity.Cinemachine;
using UnityEngine;

public class GameplayPlayerSpawner : MonoBehaviour
{
    [Header("Spawn Config")]
    [SerializeField] private Transform defaultSpawnPoint; // Fallback nếu không có vị trí từ server

    [Header("Cinemachine Camera (v3.x)")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    private GameObject spawnedPlayer;
    private GameObject spawnedCompanion;

    void Start()
    {
        SpawnCharacterAndCompanion();
    }

    private void Update()
    {
        if(cinemachineCamera == null)
        UpdateCinemachineTarget();
    }
        
    void SpawnCharacterAndCompanion()
    {
        // 1. Lấy CharacterData đã chọn từ PlayerDataManager
        var selectedCharacter = PlayerDataManager.Instance.GetSelectedCharacterData();
        var selectedCompanion = PlayerDataManager.Instance.GetSelectedCompanionData();

        if (selectedCharacter == null || selectedCompanion == null)
        {
            Debug.LogError("[GameplayPlayerSpawner] Character or Companion data not found!");
            return;
        }

        // 2. Spawn player tại vị trí mặc định trước (sẽ được cập nhật từ backend)
        Vector3 tempSpawnPos = defaultSpawnPoint != null ? defaultSpawnPoint.position : Vector3.zero;
        spawnedPlayer = Instantiate(selectedCharacter.gameplayPrefab, tempSpawnPos, Quaternion.identity);
        spawnedPlayer.name = "Player_" + selectedCharacter.characterName;

        // 3. Spawn companion offset một chút
        Vector3 companionOffset = tempSpawnPos + new Vector3(0f, 0f, 2f);
        spawnedCompanion = Instantiate(selectedCompanion.gameplayPrefab, companionOffset, Quaternion.identity);
        spawnedCompanion.name = "Companion_" + selectedCompanion.characterName;

        Debug.Log($"[GameplayPlayerSpawner] Spawned {selectedCharacter.characterName} + {selectedCompanion.characterName} at temp position.");

        // 4. Gọi PlayerLocationLoader để GET vị trí thật từ backend và cập nhật
        var locationLoader = FindFirstObjectByType<PlayerLocationLoaderFullUrl>();
        if (locationLoader != null)
        {   
            locationLoader.LoadAndApplyPosition(spawnedPlayer.transform);
            locationLoader.LoadAndApplyPosition(spawnedCompanion.transform);
            UpdateCameraTarget(spawnedPlayer.transform);
        }
        else
        {
            Debug.LogWarning("[GameplayPlayerSpawner] PlayerLocationLoaderFullUrl not found, using default spawn position.");
        }
    }

    void UpdateCinemachineTarget()
    {
        if (spawnedPlayer == null)
        {
            Debug.LogWarning("[GameplayPlayerSpawner] Cannot update Cinemachine - player not spawned");
            return;
        }

        // Tìm CinemachineCamera nếu chưa assign
        if (cinemachineCamera == null)
        {
            cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        if (cinemachineCamera == null)
        {
            Debug.LogError("[GameplayPlayerSpawner] CinemachineCamera not found in scene!");
            return;
        }

        // Update Tracking Target trong Cinemachine 3.x
        cinemachineCamera.Target.TrackingTarget = spawnedPlayer.transform;

        // Nếu muốn update LookAt target (cho Rotation Control)
        // cinemachineCamera.Target.LookAtTarget = spawnedPlayer.transform;

        Debug.Log($"[GameplayPlayerSpawner] Cinemachine tracking target updated to: {spawnedPlayer.name}");
    }

    public void UpdateCameraTarget(Transform newTarget)
    {
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Target.TrackingTarget = newTarget;
        }
    }

}
