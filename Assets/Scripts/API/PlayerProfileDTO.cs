/*using System.Collections.Generic;
using System;

[Serializable]
public class PlayerProfileDTO
{
    // Các trường backend trả về (tùy vào hệ thống của bạn)
    public string player_id;
    public string name;
    public string username_email;
    public string mail;
    public string phone;
    public string avatar_url;

    // Quy ước: companion_ids[0] = character chính, [1..] = companions
    public List<string> companion_ids = new List<string>();

    // Helpers
    public string GetMainCharacterId()
    {
        return (companion_ids != null && companion_ids.Count > 0) ? companion_ids[0] : null;
    }

    public void SetMainCharacterId(string id)
    {
        if (companion_ids == null) companion_ids = new List<string>();
        if (companion_ids.Count == 0) companion_ids.Add(id);
        else companion_ids[0] = id;
    }

    public bool HasCompanion(string id)
    {
        return companion_ids != null && companion_ids.Contains(id);
    }

    public void AddCompanion(string id)
    {
        if (companion_ids == null) companion_ids = new List<string>();
        if (!companion_ids.Contains(id))
            companion_ids.Add(id);
    }

    public void RemoveCompanion(string id)
    {
        if (companion_ids == null) return;
        int idx = companion_ids.IndexOf(id);
        // Không xóa index 0 (character chính) bằng API này
        if (idx > 0) companion_ids.RemoveAt(idx);
    }
}
*/