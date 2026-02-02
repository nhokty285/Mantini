/*using UnityEngine;

public class NPCNameplate : MonoBehaviour
{
    [SerializeField] private string npcName = "NPC";
    void Start()
    {
        // Gọi Manager để xin 1 cái tên
        NameplateManager.Instance.Register(this.transform, npcName);
    }

    void OnDestroy()
    {
        // Báo Manager thu hồi tên khi NPC bị hủy
        if (NameplateManager.Instance != null)
        {
            NameplateManager.Instance.Unregister(this.transform);
        }
    }
}
*/