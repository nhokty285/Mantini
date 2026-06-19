using System.Collections.Generic;
using UnityEngine;

public class NPCManager : MonoBehaviour
{
    public static NPCManager Instance { get; private set; }

    [Header("NPC Management")]
    [SerializeField] private List<BaseNPC> allNPCs = new();

    // Naming: private field dùng _camelCase theo Mantini coding standards
    private readonly Dictionary<string, BaseNPC> _npcDictionary = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeNPCs();
    }

    private void InitializeNPCs()
    {
        // Tìm tất cả NPCs trong scene
        BaseNPC[] foundNPCs = FindObjectsByType<BaseNPC>(FindObjectsSortMode.InstanceID);
        foreach (var npc in foundNPCs)
            RegisterNPC(npc);

        GameLog.Info($"[NPCManager] Initialized with {_npcDictionary.Count} NPCs");
    }

    public void RegisterNPC(BaseNPC npc)
    {
        if (npc == null || string.IsNullOrEmpty(npc.GetNPCId())) return;

        _npcDictionary[npc.GetNPCId()] = npc;

        // Subscribe to NPC interaction events
        npc.OnPlayerInteraction += HandleNPCInteraction;

        // Auto-add NPCChatAdapter nếu chưa có, đồng thời wire targetNPC qua public setter
        var chatAdapter = npc.GetComponent<NPCChatAdapter>();
        if (chatAdapter == null)
        {
            chatAdapter = npc.gameObject.AddComponent<NPCChatAdapter>();
#if UNITY_EDITOR
            GameLog.Info($"[NPCManager] Auto-added NPCChatAdapter to {npc.GetNPCName()}");
#endif
        }
        chatAdapter.SetTargetNPC(npc); // dùng public setter thay vì reflection

        if (!allNPCs.Contains(npc))
            allNPCs.Add(npc);

#if UNITY_EDITOR
        GameLog.Info($"[NPCManager] Registered NPC: {npc.GetNPCName()} ({npc.GetNPCType()})");
#endif
    }

    private void HandleNPCInteraction(bool isEntering, string npcName, BaseNPC npc)
    {
        if (MainMenuView.Instance == null || npc == null)
        {
            Debug.LogError($"[NPCManager] HandleNPCInteraction: MainMenuView or NPC is null. " +
                           $"MainMenuView={MainMenuView.Instance}, NPC={npc}");
            return;
        }

        // Refactor: switch + pattern matching, gọn hơn if-else-if và rõ ý định hơn
        switch (npc)
        {
            case VendorNPC vendor:
                MainMenuView.Instance.SetNPCInteraction(isEntering, npcName, vendor.GetShopData(), npc);
                break;
            case CompanionNPC _:
                MainMenuView.Instance.SetNPCInteraction(isEntering, npcName, null, npc);
                break;
        }
    }

    // Refactor: TryGetValue O(1) thay vì ContainsKey + indexer (2 lookups)
    public BaseNPC GetNPC(string npcId)
        => _npcDictionary.TryGetValue(npcId, out var npc) ? npc : null;

    public List<BaseNPC> GetNPCsByType(NPCType type)
        => allNPCs.FindAll(npc => npc.GetNPCType() == type);

    public void ProcessNPCInteraction(string npcId)
        => GetNPC(npcId)?.ProcessInteraction();
}