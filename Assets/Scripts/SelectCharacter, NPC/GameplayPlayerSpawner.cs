/*using Unity.Cinemachine;
using UnityEngine;
using TMPro;

public class GameplayPlayerSpawner : MonoBehaviour
{
    [Header("Spawn Config")]
    [SerializeField] private Transform defaultSpawnPoint;

    [Header("Cinemachine Camera (v3.x)")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    private GameObject spawnedPlayer;
    private GameObject spawnedCompanion;
    private TextMeshProUGUI playerNameText; // Cache reference tới text component

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
        var selectedCharacter = PlayerDataManager.Instance.GetSelectedCharacterData();
        var selectedCompanion = PlayerDataManager.Instance.GetSelectedCompanionData();

        if (selectedCharacter == null || selectedCompanion == null)
        {
            Debug.LogError("[GameplayPlayerSpawner] Character or Companion data not found!");
            return;
        }

        Vector3 tempSpawnPos = defaultSpawnPoint != null ? defaultSpawnPoint.position : Vector3.zero;
        spawnedPlayer = Instantiate(selectedCharacter.gameplayPrefab, tempSpawnPos, Quaternion.identity);
        spawnedPlayer.name = "Player_" + selectedCharacter.characterName;
        
        // ✅ Cập nhật text hiển thị lần đầu
        SetPlayerNameInUI(selectedCharacter.characterName);
        
        Vector3 companionOffset = tempSpawnPos + new Vector3(0f, 0f, 2f);
        spawnedCompanion = Instantiate(selectedCompanion.gameplayPrefab, companionOffset, Quaternion.identity);
        spawnedCompanion.name = "Companion_" + selectedCompanion.characterName;

        Debug.Log($"[GameplayPlayerSpawner] Spawned {selectedCharacter.characterName} + {selectedCompanion.characterName} at temp position.");

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

        if (cinemachineCamera == null)
        {
            cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        if (cinemachineCamera == null)
        {
            Debug.LogError("[GameplayPlayerSpawner] CinemachineCamera not found in scene!");
            return;
        }

        cinemachineCamera.Target.TrackingTarget = spawnedPlayer.transform;
        Debug.Log($"[GameplayPlayerSpawner] Cinemachine tracking target updated to: {spawnedPlayer.name}");
    }

    public void UpdateCameraTarget(Transform newTarget)
    {
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Target.TrackingTarget = newTarget;
        }
    }

    /// <summary>
    /// Cập nhật tên của spawned player khi user lưu profil mới
    /// </summary>
    public void UpdatePlayerName(string newName)
    {
        if (spawnedPlayer == null)
        {
            Debug.LogWarning("[GameplayPlayerSpawner] spawnedPlayer is not spawned yet!");
            return;
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            Debug.LogWarning("[GameplayPlayerSpawner] New name is empty!");
            return;
        }

        // 1️⃣ Cập nhật GameObject name
        spawnedPlayer.name = "Player_" + newName;
        
        // 2️⃣ Cập nhật text hiển thị bên trong prefab
        SetPlayerNameInUI(newName);
        
        // 3️⃣ Cập nhật NameplateManager nếu có
        var manager = FindFirstObjectByType<NameplateManager>();
        if (manager != null)
        {
            manager.UpdateNameplateText(spawnedPlayer.transform, newName);
            Debug.Log($"[GameplayPlayerSpawner] Updated NameplateManager for: {newName}");
        }

        Debug.Log($"[GameplayPlayerSpawner] Player name and display updated to: {newName}");
    }

    /// <summary>
    /// Tìm và cập nhật tất cả TextMeshPro text components trong player prefab
    /// </summary>
    private void SetPlayerNameInUI(string playerName)
    {
        if (spawnedPlayer == null)
        {
            Debug.LogWarning("[GameplayPlayerSpawner] spawnedPlayer is null!");
            return;
        }

        // ✅ Cách 1: Tìm text component có tag "PlayerNameText" hoặc tên cụ thể
        TextMeshProUGUI[] textComponents = spawnedPlayer.GetComponentsInChildren<TextMeshProUGUI>();
        
        if (textComponents.Length == 0)
        {
            Debug.LogWarning("[GameplayPlayerSpawner] No TextMeshProUGUI found in player prefab!");
            return;
        }

        bool foundPlayerNameText = false;

        foreach (var textComponent in textComponents)
        {
            // Kiểm tra GameObject có tên chứa "Name", "PlayerName", hoặc "Nameplate"
            if (textComponent.gameObject.name.Contains("Name") || 
                textComponent.gameObject.name.Contains("Nameplate") ||
                textComponent.gameObject.name.Contains("PlayerName"))
            {
                textComponent.text = playerName;
                playerNameText = textComponent; // Cache để dùng lại
                foundPlayerNameText = true;
                
                Debug.Log($"[GameplayPlayerSpawner] Updated text '{textComponent.gameObject.name}' to: {playerName}");
                break; // Thoát sau khi tìm text chính
            }
        }

        if (!foundPlayerNameText)
        {
            // Fallback: Nếu không tìm được by name, cập nhật text đầu tiên tìm được
            Debug.LogWarning("[GameplayPlayerSpawner] Could not find 'PlayerName' text by name convention. Updating first TextMeshPro found.");
            textComponents[0].text = playerName;
            playerNameText = textComponents[0];
        }
    }
}
*/

using Unity.Cinemachine;
using UnityEngine;
using TMPro;

public class GameplayPlayerSpawner : MonoBehaviour
{
    [Header("Spawn Config")]
    [SerializeField] private Transform defaultSpawnPoint;

    [Header("Cinemachine Camera (v3.x)")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Header("API Service")]
    [SerializeField] private PlayerApiService playerApiService;

    private GameObject spawnedPlayer;
    private GameObject spawnedCompanion;
    private TextMeshProUGUI playerNameText; // Cache reference tới text component

    void Start()
    {
        SpawnCharacterAndCompanion();

        // ✅ Sau khi spawn, gọi API để lấy profile mới nhất từ server
        LoadPlayerProfileFromServer();
    }

    private void Update()
    {
        if (cinemachineCamera == null)
            UpdateCinemachineTarget();
    }

    void SpawnCharacterAndCompanion()
    {
        var selectedCharacter = PlayerDataManager.Instance.GetSelectedCharacterData();
        var selectedCompanion = PlayerDataManager.Instance.GetSelectedCompanionData();

        if (selectedCharacter == null || selectedCompanion == null)
        {
            Debug.LogError("[GameplayPlayerSpawner] Character or Companion data not found!");
            return;
        }

        Vector3 tempSpawnPos = defaultSpawnPoint != null ? defaultSpawnPoint.position : Vector3.zero;
        spawnedPlayer = Instantiate(selectedCharacter.gameplayPrefab, tempSpawnPos, Quaternion.identity);
        spawnedPlayer.name = "Player_" + selectedCharacter.characterName;

        // ✅ Cập nhật text hiển thị lần đầu (dùng character name)
        SetPlayerNameInUI(selectedCharacter.characterName);

        Vector3 companionOffset = tempSpawnPos + new Vector3(0f, 0f, 2f);
        spawnedCompanion = Instantiate(selectedCompanion.gameplayPrefab, companionOffset, Quaternion.identity);
        spawnedCompanion.name = "Companion_" + selectedCompanion.characterName;

        Debug.Log($"[GameplayPlayerSpawner] Spawned {selectedCharacter.characterName} + {selectedCompanion.characterName} at temp position.");

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

    /// <summary>
    /// ✅ GỌI API ĐỂ LẤY PROFILE TỪ SERVER
    /// Khi player spawn ra, tải tên mới nhất từ server
    /// </summary>
    private void LoadPlayerProfileFromServer()
    {
        if (playerApiService == null)
        {
            playerApiService = FindFirstObjectByType<PlayerApiService>();
        }

        if (playerApiService == null)
        {
            Debug.LogWarning("[GameplayPlayerSpawner] PlayerApiService not found! Skipping profile load.");
            return;
        }

        // Gọi API để lấy profile từ server
        playerApiService.LoadProfileFromServer(
            onSuccess: (profileData) =>
            {
                // ✅ API trả về data thành công
                if (profileData != null && !string.IsNullOrEmpty(profileData.name))
                {
                    Debug.Log($"[GameplayPlayerSpawner] Loaded profile from server: {profileData.name}");

                    // Cập nhật tên player với dữ liệu từ server
                    UpdatePlayerNameFromServer(profileData.name);
                }
                else
                {
                    Debug.LogWarning("[GameplayPlayerSpawner] Profile data is empty or null");
                }
            },
            onError: (error) =>
            {
                // ❌ API thất bại, giữ lại tên hiện tại
                Debug.LogError($"[GameplayPlayerSpawner] Failed to load profile from server: {error}");
            }
        );
    }

    /// <summary>
    /// ✅ CẬP NHẬT TÊN PLAYER TỪ DỮ LIỆU SERVER
    /// Được gọi sau khi API trả về dữ liệu thành công
    /// </summary>
    private void UpdatePlayerNameFromServer(string serverPlayerName)
    {
        if (spawnedPlayer == null)
        {
            Debug.LogWarning("[GameplayPlayerSpawner] spawnedPlayer is not spawned yet!");
            return;
        }

        if (string.IsNullOrWhiteSpace(serverPlayerName))
        {
            Debug.LogWarning("[GameplayPlayerSpawner] Server player name is empty!");
            return;
        }

        // 1️⃣ Cập nhật GameObject name
        spawnedPlayer.name = "Player_" + serverPlayerName;

        // 2️⃣ Cập nhật text hiển thị bên trong prefab
        SetPlayerNameInUI(serverPlayerName);

        // 3️⃣ Cập nhật NameplateManager nếu có
        var manager = FindFirstObjectByType<NameplateManager>();
        if (manager != null)
        {
            manager.UpdateNameplateText(spawnedPlayer.transform, serverPlayerName);
            Debug.Log($"[GameplayPlayerSpawner] Updated NameplateManager for: {serverPlayerName}");
        }

        Debug.Log($"[GameplayPlayerSpawner] ✅ Player name updated from server to: {serverPlayerName}");
    }
    /// <summary>
    /// Tìm và cập nhật tất cả TextMeshPro text components trong player prefab
    /// </summary>
    private void SetPlayerNameInUI(string playerName)
    {
        if (spawnedPlayer == null)
        {
            Debug.LogWarning("[GameplayPlayerSpawner] spawnedPlayer is null!");
            return;
        }

        // Tìm tất cả TextMeshProUGUI components
        TextMeshProUGUI[] textComponents = spawnedPlayer.GetComponentsInChildren<TextMeshProUGUI>();

        if (textComponents.Length == 0)
        {
            Debug.LogWarning("[GameplayPlayerSpawner] No TextMeshProUGUI found in player prefab!");
            return;
        }

        bool foundPlayerNameText = false;

        foreach (var textComponent in textComponents)
        {
            // Kiểm tra GameObject có tên chứa "Name", "PlayerName", hoặc "Nameplate"
            if (textComponent.gameObject.name.Contains("Name") ||
                textComponent.gameObject.name.Contains("Nameplate") ||
                textComponent.gameObject.name.Contains("PlayerName"))
            {
                textComponent.text = playerName;
                playerNameText = textComponent; // Cache để dùng lại
                foundPlayerNameText = true;

                Debug.Log($"[GameplayPlayerSpawner] Updated text '{textComponent.gameObject.name}' to: {playerName}");
                break; // Thoát sau khi tìm text chính
            }
        }

        if (!foundPlayerNameText)
        {
            // Fallback: Nếu không tìm được by name, cập nhật text đầu tiên tìm được
            Debug.LogWarning("[GameplayPlayerSpawner] Could not find 'PlayerName' text by name convention. Updating first TextMeshPro found.");
            textComponents[0].text = playerName;
            playerNameText = textComponents[0];
        }
    }

    void UpdateCinemachineTarget()
    {
        if (spawnedPlayer == null)
        {
            Debug.LogWarning("[GameplayPlayerSpawner] Cannot update Cinemachine - player not spawned");
            return;
        }

        if (cinemachineCamera == null)
        {
            cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        if (cinemachineCamera == null)
        {
            Debug.LogError("[GameplayPlayerSpawner] CinemachineCamera not found in scene!");
            return;
        }

        cinemachineCamera.Target.TrackingTarget = spawnedPlayer.transform;
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