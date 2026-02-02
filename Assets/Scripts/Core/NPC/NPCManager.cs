using System.Collections.Generic;
using UnityEngine;

public class NPCManager : MonoBehaviour
{
    public static NPCManager Instance { get; private set; }

    [Header("NPC Management")]
    [SerializeField] private List<BaseNPC> allNPCs = new();

    private Dictionary<string, BaseNPC> npcDictionary = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeNPCs();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeNPCs()
    {
        // Tìm tất cả NPCs trong scene
        BaseNPC[] foundNPCs = FindObjectsByType<BaseNPC>(FindObjectsSortMode.InstanceID);
        /*FindObjectsOfType<BaseNPC>() */
        foreach (var npc in foundNPCs)
        {
            RegisterNPC(npc);
        }

        Debug.Log($"NPCManager initialized with {npcDictionary.Count} NPCs");
    }

    public void RegisterNPC(BaseNPC npc)
    {
        if (npc != null && !string.IsNullOrEmpty(npc.GetNPCId()))
        {
            npcDictionary[npc.GetNPCId()] = npc;

            // Subscribe to NPC interaction events
            npc.OnPlayerInteraction += HandleNPCInteraction;

            var chatAdapter = npc.GetComponent<NPCChatAdapter>();
            if (chatAdapter == null)
            {
                chatAdapter = npc.gameObject.AddComponent<NPCChatAdapter>();
                Debug.Log($"✅ Auto-added NPCChatAdapter to {npc.GetNPCName()}");
            }

            SetupNPCChatAdapter(chatAdapter, npc);

            if (!allNPCs.Contains(npc))
                allNPCs.Add(npc);

            Debug.Log($"Registered NPC: {npc.GetNPCName()} ({npc.GetNPCType()})");
        }
    }

    // ✅ NEW METHOD: Setup NPCChatAdapter
    private void SetupNPCChatAdapter(NPCChatAdapter adapter, BaseNPC npc)
    {
        // Use reflection to set private/protected targetNPC field
        /*  var field = typeof(NPCChatAdapter).GetField("targetNPC",
              System.Reflection.BindingFlags.NonPublic |
              System.Reflection.BindingFlags.Instance);

          if (field != null)
          {
              field.SetValue(adapter, npc);
              Debug.Log($"✅ Setup NPCChatAdapter for {npc.GetNPCName()}");
          }
          else
          {
              Debug.LogError($"❌ Cannot setup NPCChatAdapter for {npc.GetNPCName()} - targetNPC field not found");
          }*/

        if (adapter == null || npc == null)
        {
            Debug.LogError("❌ Adapter or NPC is null in SetupNPCChatAdapter");
            return;
        }

        // ✅ Sử dụng public method thay vì reflection
        adapter.SetTargetNPC(npc);
        Debug.Log($"✅ Setup NPCChatAdapter for {npc.GetNPCName()}");
    }
    private void HandleNPCInteraction(bool isEntering, string npcName, BaseNPC npc)
    {
        // Xử lý interaction events từ NPCs
        /*if (MainMenuView.Instance != null)
        {
            if (npc is VendorNPC vendor)
            {
                // Vendor interaction
                MainMenuView.Instance.SetNPCInteraction(isEntering, npcName, vendor.GetShopData(), npc);
            }
            else if (npc is CompanionNPC companion)
            {
                // Companion interaction - không cần shop data
                MainMenuView.Instance.SetNPCInteraction(isEntering, npcName, null, npc);
            }
        }*/

        Debug.Log($"🔍 HandleNPCInteraction: isEntering={isEntering}, npc={npc?.name}, npcName={npcName}");

        if (MainMenuView.Instance != null && npc != null)
        {
            if (npc is VendorNPC vendor)
            {
                MainMenuView.Instance.SetNPCInteraction(isEntering, npcName, vendor.GetShopData(), npc);
            }
            else if (npc is CompanionNPC companion)
            {
                MainMenuView.Instance.SetNPCInteraction(isEntering, npcName, null, npc);
            }
        }
        else
        {
            Debug.LogError($"❌ MainMenuView or NPC is null! MainMenuView={MainMenuView.Instance}, NPC={npc}");
        }
    }

    public BaseNPC GetNPC(string npcId)
    {
        return npcDictionary.ContainsKey(npcId) ? npcDictionary[npcId] : null;
    }

    public List<BaseNPC> GetNPCsByType(NPCType type)
    {
        return allNPCs.FindAll(npc => npc.GetNPCType() == type);
    }

    public void ProcessNPCInteraction(string npcId)
    {
        var npc = GetNPC(npcId);
        npc?.ProcessInteraction();
    }
}
