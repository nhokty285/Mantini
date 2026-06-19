using Unity.Cinemachine;
using UnityEngine;
using TMPro;
using System.Collections;

public class GameplayPlayerSpawner : MonoBehaviour
{
    [Header("Spawn Config")]
    [SerializeField] private Transform defaultSpawnPoint;

    [Header("Cinemachine Camera (v3.x)")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Header("API Service")]
    [SerializeField] private PlayerApiService playerApiService;

    private GameObject _spawnedPlayer;
    private GameObject _spawnedCompanion;
    private TextMeshProUGUI _playerNameText;
    private bool _cameraTargetEnsured = false; // Refactor: tránh StartCoroutine mỗi frame trong Update

    public static GameplayPlayerSpawner Instance { get; private set; }
    public GameObject SpawnedPlayer => _spawnedPlayer;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SpawnCharacterAndCompanion();
        LoadPlayerProfileFromServer();
        ApplyDefaultAvatarIfNeeded();

        // Refactor: chạy 1 lần thay vì StartCoroutine mỗi frame trong Update (BUG cũ gây spawn coroutine vô hạn)
        StartCoroutine(EnsureCameraTarget());
    }

    private void SpawnCharacterAndCompanion()
    {
        var selectedCharacter = PlayerDataManager.Instance.GetSelectedCharacterData();
        var selectedCompanion = PlayerDataManager.Instance.GetSelectedCompanionData();

        if (selectedCharacter == null || selectedCompanion == null)
        {
            Debug.LogError("[GameplayPlayerSpawner] Character or Companion data not found!");
            return;
        }

        Vector3 tempSpawnPos = defaultSpawnPoint != null ? defaultSpawnPoint.position : Vector3.zero;
        _spawnedPlayer = Instantiate(selectedCharacter.gameplayPrefab, tempSpawnPos, Quaternion.identity);
        _spawnedPlayer.name = "Player_" + selectedCharacter.characterName;
        _spawnedPlayer.tag = "Player";
        SetPlayerNameInUI(selectedCharacter.characterName);

        Vector3 companionOffset = tempSpawnPos + new Vector3(0f, 0f, 2f);
        _spawnedCompanion = Instantiate(selectedCompanion.gameplayPrefab, companionOffset, Quaternion.identity);
        _spawnedCompanion.name = "Companion_" + selectedCompanion.characterName;

#if UNITY_EDITOR
        GameLog.Info($"[GameplayPlayerSpawner] Spawned {selectedCharacter.characterName} + {selectedCompanion.characterName} at temp position.");
#endif

        var locationLoader = FindFirstObjectByType<PlayerLocationLoaderFullUrl>();
        if (locationLoader != null)
        {
            locationLoader.LoadAndApplyPosition(_spawnedPlayer.transform);
            locationLoader.LoadAndApplyPosition(_spawnedCompanion.transform);
            UpdateCameraTarget(_spawnedPlayer.transform);
        }
        else
        {
            GameLog.Warn("[GameplayPlayerSpawner] PlayerLocationLoaderFullUrl not found, using default spawn position.");
        }
    }

    private IEnumerator EnsureCameraTarget()
    {
        if (_cameraTargetEnsured) yield break;
        var wait = new WaitForSeconds(0.5f);

        for (int i = 0; i < 10 && cinemachineCamera == null; i++)
        {
            cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
            if (cinemachineCamera != null)
            {
                UpdateCinemachineTarget();
                _cameraTargetEnsured = true;
                yield break;
            }
            yield return wait;
        }
    }

    private void LoadPlayerProfileFromServer()
    {
        if (playerApiService == null)
            playerApiService = FindFirstObjectByType<PlayerApiService>();

        if (playerApiService == null)
        {
            GameLog.Warn("[GameplayPlayerSpawner] PlayerApiService not found! Skipping profile load.");
            return;
        }

        playerApiService.LoadProfileFromServer(
            onSuccess: (profileData) =>
            {
                if (profileData != null && !string.IsNullOrEmpty(profileData.name))
                {
#if UNITY_EDITOR
                    GameLog.Info($"[GameplayPlayerSpawner] Loaded profile from server: {profileData.name}");
#endif
                    UpdatePlayerNameFromServer(profileData.name);
                }
                else
                {
                    GameLog.Warn("[GameplayPlayerSpawner] Profile data is empty or null");
                }
            },
            onError: (error) =>
            {
                Debug.LogError($"[GameplayPlayerSpawner] Failed to load profile from server: {error}");
            }
        );
    }

    private void ApplyDefaultAvatarIfNeeded()
    {
        var profileData = FindAnyObjectByType<ProfileData>();
        if (profileData == null)
        {
            GameLog.Warn("[GameplayPlayerSpawner] ProfileData not found, skip avatar fallback.");
            return;
        }

        if (profileData.AvatarSprite != null) return;

        var selectedCharacter = PlayerDataManager.Instance.GetSelectedCharacterData();
        if (selectedCharacter == null || selectedCharacter.characterIcon == null)
        {
            GameLog.Warn("[GameplayPlayerSpawner] No characterIcon found in CharacterData.");
            return;
        }

        profileData.SetDefaultAvatar(selectedCharacter.characterIcon);
#if UNITY_EDITOR
        GameLog.Info($"[GameplayPlayerSpawner] Applied default avatar from CharacterData: {selectedCharacter.characterName}");
#endif
    }

    private void UpdatePlayerNameFromServer(string serverPlayerName)
    {
        if (_spawnedPlayer == null)
        {
            GameLog.Warn("[GameplayPlayerSpawner] spawnedPlayer is not spawned yet!");
            return;
        }

        if (string.IsNullOrWhiteSpace(serverPlayerName))
        {
            GameLog.Warn("[GameplayPlayerSpawner] Server player name is empty!");
            return;
        }

        _spawnedPlayer.name = "Player_" + serverPlayerName;
        SetPlayerNameInUI(serverPlayerName);

        var manager = FindFirstObjectByType<NameplateManager>();
        if (manager != null)
        {
            manager.UpdateNameplateText(_spawnedPlayer.transform, serverPlayerName);
#if UNITY_EDITOR
            GameLog.Info($"[GameplayPlayerSpawner] Updated NameplateManager for: {serverPlayerName}");
#endif
        }

#if UNITY_EDITOR
        GameLog.Info($"[GameplayPlayerSpawner] ✅ Player name updated from server to: {serverPlayerName}");
#endif
    }

    private void SetPlayerNameInUI(string playerName)
    {
        if (_spawnedPlayer == null)
        {
            GameLog.Warn("[GameplayPlayerSpawner] spawnedPlayer is null!");
            return;
        }

        TextMeshProUGUI[] textComponents = _spawnedPlayer.GetComponentsInChildren<TextMeshProUGUI>();

        if (textComponents.Length == 0)
        {
            GameLog.Warn("[GameplayPlayerSpawner] No TextMeshProUGUI found in player prefab!");
            return;
        }

        foreach (var textComponent in textComponents)
        {
            string goName = textComponent.gameObject.name;
            if (goName.Contains("Name") || goName.Contains("Nameplate") || goName.Contains("PlayerName"))
            {
                textComponent.text = playerName;
                _playerNameText = textComponent;
#if UNITY_EDITOR
                GameLog.Info($"[GameplayPlayerSpawner] Updated text '{goName}' to: {playerName}");
#endif
                return;
            }
        }

        // Fallback: dùng text đầu tiên nếu không match convention
        GameLog.Warn("[GameplayPlayerSpawner] Could not find 'PlayerName' text by name convention. Updating first TextMeshPro found.");
        textComponents[0].text = playerName;
        _playerNameText = textComponents[0];
    }

    private void UpdateCinemachineTarget()
    {
        if (_spawnedPlayer == null)
        {
            GameLog.Warn("[GameplayPlayerSpawner] Cannot update Cinemachine - player not spawned");
            return;
        }

        if (cinemachineCamera == null)
            cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();

        if (cinemachineCamera == null)
        {
            Debug.LogError("[GameplayPlayerSpawner] CinemachineCamera not found in scene!");
            return;
        }

        cinemachineCamera.Target.TrackingTarget = _spawnedPlayer.transform;
#if UNITY_EDITOR
        GameLog.Info($"[GameplayPlayerSpawner] Cinemachine tracking target updated to: {_spawnedPlayer.name}");
#endif
    }

    public void UpdateCameraTarget(Transform newTarget)
    {
        if (cinemachineCamera != null)
            cinemachineCamera.Target.TrackingTarget = newTarget;
    }
}