using System;
using UnityEngine;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance { get; private set; }

    [Header("Bootstrap Catalog")]
    [SerializeField] private CharacterCatalog catalog;

    // Nguồn dữ liệu trung tâm - nạp từ CharacterCatalog (single source of truth)
    private CharacterData[] _characterDataArray;
    private CharacterData[] _companionDataArray;

    // Read-only accessor cho các scene chọn nhân vật (không giữ mảng riêng nữa)
    public CharacterData[] PlayerCharacters => _characterDataArray;
    public CharacterData[] Companions => _companionDataArray;

    const string KEY_CHAR = "SelectedPlayerCharacter";
    const string KEY_COMP = "SelectedCompanion";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Bootstrap: đăng ký data ngay khi app khởi động, độc lập scene chọn NV.
        // Cho phép returning player vào thẳng MapTest2 mà arrays vẫn sẵn sàng.
        if (catalog != null)
        {
            if (catalog.playerCharacters != null) _characterDataArray = catalog.playerCharacters;
            if (catalog.companions != null)       _companionDataArray = catalog.companions;
        }
        else
        {
            Debug.LogError("[PlayerDataManager] CharacterCatalog chưa được gán!");
        }
    }

    // Lưu lựa chọn
    public void SaveCharacterIndex(int idx)
    {
        PlayerPrefs.SetInt(KEY_CHAR, idx);
        PlayerPrefs.Save();
    }

    public void SaveCompanionIndex(int idx)
    {
        PlayerPrefs.SetInt(KEY_COMP, idx);
        PlayerPrefs.Save();
    }

    // Khôi phục lựa chọn từ server (theo characterName). O(n) trên mảng nhỏ, không GC.
    public void SetSelectedCharacterByName(string characterName)
    {
        int idx = IndexOfByName(_characterDataArray, characterName);
        if (idx < 0)
        {
            GameLog.Warn($"[PlayerDataManager] Character '{characterName}' not in catalog");
            return;
        }
        SaveCharacterIndex(idx);
    }

    public void SetSelectedCompanionByName(string companionName)
    {
        int idx = IndexOfByName(_companionDataArray, companionName);
        if (idx < 0)
        {
            GameLog.Warn($"[PlayerDataManager] Companion '{companionName}' not in catalog");
            return;
        }
        SaveCompanionIndex(idx);
    }

    private static int IndexOfByName(CharacterData[] arr, string name)
    {
        if (arr == null) return -1;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] != null && arr[i].characterName == name) return i;
        return -1;
    }

    public CharacterData GetSelectedCharacterData()
    {
        if (_characterDataArray == null || _characterDataArray.Length == 0)
        {
            Debug.LogError("[PlayerDataManager] Character data not registered");
            return null;
        }

        int idx = PlayerPrefs.GetInt(KEY_CHAR, 0);

        if (idx < 0 || idx >= _characterDataArray.Length)
        {
            GameLog.Warn($"[PlayerDataManager] Invalid character index {idx}. Resetting to 0");
            idx = 0;
            SaveCharacterIndex(0); // Persist correction
        }

        return _characterDataArray[idx];
    }


    public CharacterData GetSelectedCompanionData()
    {
        int idx = PlayerPrefs.GetInt(KEY_COMP, 0);
        return (_companionDataArray != null && idx >= 0 && idx < _companionDataArray.Length)
            ? _companionDataArray[idx]
            : null;
    }

    // Truy xuất Gameplay Prefab (cho spawner)
    public GameObject GetSelectedCharacterPrefab()
    {
        var data = GetSelectedCharacterData();
        return data?.gameplayPrefab;
    }

    public GameObject GetSelectedCompanionPrefab()
    {
        var data = GetSelectedCompanionData();
        return data?.gameplayPrefab;
    }

    // Truy xuất Preview Prefab (cho selection screen)
    public GameObject GetCharacterPreviewPrefab(int index)
    {
        if (_characterDataArray != null && index >= 0 && index < _characterDataArray.Length)
            return _characterDataArray[index].previewPrefab;
        return null;
    }

    public GameObject GetCompanionPreviewPrefab(int index)
    {
        if (_companionDataArray != null && index >= 0 && index < _companionDataArray.Length)
            return _companionDataArray[index].previewPrefab;
        return null;
    }
}
