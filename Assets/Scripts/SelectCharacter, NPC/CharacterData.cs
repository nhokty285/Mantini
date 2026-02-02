using UnityEngine;

[CreateAssetMenu(fileName = "New Character Data", menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Character Info")]
    public string characterName;
    [TextArea(2, 4)]
    public string description;

    [Header("Preview (Selection Screen)")]
    public GameObject previewPrefab; // Prefab chứa RawImage cho UI selection
    public Sprite characterIcon; // Icon/Avatar cho UI

    [Header("Gameplay (In-Game)")]
    public GameObject gameplayPrefab; // Character 3D thật sẽ spawn trong game

    [Header("Stats")]
    public float moveSpeed = 5f;
    public int maxHealth = 100;
}
