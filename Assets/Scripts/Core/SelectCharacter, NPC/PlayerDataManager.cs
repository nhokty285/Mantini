/*// PlayerDataManager.cs
using UnityEngine;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance { get; private set; }

    // Nguồn dữ liệu trung tâm – được set từ scene chọn
    private GameObject[] _characterPrefabs;
    private GameObject[] _companionPrefabs;

    const string KEY_CHAR = "SelectedPlayerCharacter";
    const string KEY_COMP = "SelectedCompanion";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Đăng ký dữ liệu 1 lần từ scene chọn
    public void RegisterCharacterPrefabs(GameObject[] list) => _characterPrefabs = list;
    public void RegisterCompanionPrefabs(GameObject[] list) => _companionPrefabs = list;

    // Lưu lựa chọn
    public void SaveCharacterIndex(int idx) { PlayerPrefs.SetInt(KEY_CHAR, idx); PlayerPrefs.Save(); }
    public void SaveCompanionIndex(int idx) { PlayerPrefs.SetInt(KEY_COMP, idx); PlayerPrefs.Save(); }

    // Truy xuất prefab đã chọn
    public GameObject GetSelectedCharacterPrefab()
    {
        int idx = PlayerPrefs.GetInt(KEY_CHAR, 0);
        return (_characterPrefabs != null && idx >= 0 && idx < _characterPrefabs.Length) ? _characterPrefabs[idx] : null;
    }
    public GameObject GetSelectedCompanionPrefab()
    {
        int idx = PlayerPrefs.GetInt(KEY_COMP, 0);
        return (_companionPrefabs != null && idx >= 0 && idx < _companionPrefabs.Length) ? _companionPrefabs[idx] : null;
    }
}
*/

using System;
using UnityEngine;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance { get; private set; }

    // Nguồn dữ liệu trung tâm - CharacterData thay vì GameObject[]
    private CharacterData[] _characterDataArray;
    private CharacterData[] _companionDataArray;

    const string KEY_CHAR = "SelectedPlayerCharacter";
    const string KEY_COMP = "SelectedCompanion";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Đăng ký CharacterData thay vì GameObject[]
    public void RegisterCharacterData(CharacterData[] data) => _characterDataArray = data;
    public void RegisterCompanionData(CharacterData[] data) => _companionDataArray = data;

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
            Debug.LogWarning($"[PlayerDataManager] Invalid character index {idx}. Resetting to 0");
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
