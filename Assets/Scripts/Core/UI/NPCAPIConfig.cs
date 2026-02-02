using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NPCAPIConfig", menuName = "Shop/NPCAPIConfig")]
public class NPCAPIConfig : ScriptableObject
{
    [Header("NPC Information")]
    public string npcId;
    public string npcName;
    public string shopCategory; // "Fashion", "Electronics", "Food", "Books", etc.

    [Header("API Configuration")]
    public string apiUrl;
    public int maxItems = 10;
    public List<string> requiredBrands = new List<string>(); // Filter theo brand
    public List<string> requiredKeywords = new List<string>(); // Filter theo keywords
    public ItemType primaryItemType = ItemType.Shoes; // Loại item chính của NPC này

    [Header("Backup Configuration")]
    public bool useBackupAPI = false;
    public string backupApiUrl;
}
