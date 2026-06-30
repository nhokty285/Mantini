using UnityEngine;

[CreateAssetMenu(fileName = "CharacterCatalog", menuName = "Game/Character Catalog")]
public class CharacterCatalog : ScriptableObject
{
    [Header("Player Characters")]
    public CharacterData[] playerCharacters;

    [Header("Companions")]
    public CharacterData[] companions;
}
