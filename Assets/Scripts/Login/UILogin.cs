using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UILogin : MonoBehaviour
{
    [SerializeField] private Button loginButton;

    [SerializeField] private TMP_Text userIdText;
    [SerializeField] private TMP_Text userNameText;

    [SerializeField] private Transform loginPanel, userPanel;

    [SerializeField] public LoginController loginController;

    private PlayerProfile playerProfile;
    [SerializeField] private Onboarding onboarding;

    [SerializeField] private PlayerApiService playerApiService;

    private void Start()
    {
     /*   FirebaseGoogleLogin.Instance.OnLoginSuccess += (user) => {
            // ẩn panel login + vào game
            onboarding.StartOnboarding();
        };*/
    }

    private void OnEnable()
    {
        loginButton.onClick.RemoveAllListeners();
        loginButton.onClick.AddListener(LoginButtonPressed);
        loginController.OnSignedIn += LoginController_OnSignedIn;
    }

    private void OnDisable()
    {
        loginButton.onClick.RemoveListener(LoginButtonPressed);
        loginController.OnSignedIn -= LoginController_OnSignedIn;
    }

    private async void LoginButtonPressed()
    {
        await loginController.InitSignIn();
    }

    private void LoginController_OnSignedIn(PlayerProfile profile)
    {
        playerProfile = profile;

        loginPanel.gameObject.SetActive(false);

        if (playerApiService == null)
        {
            GameLog.Warn("[UILogin] PlayerApiService null, fallback onboarding");
            onboarding.StartOnboarding();
            return;
        }

        // Player cũ (avatar_id khác rỗng) → khôi phục lựa chọn rồi vào thẳng MapTest2.
        // Player mới / lỗi GET → vào onboarding.
        playerApiService.LoadProfileFromServer(
            onSuccess: data =>
            {
                if (data != null && !string.IsNullOrEmpty(data.avatar_id))
                {
                    PlayerDataManager.Instance.SetSelectedCharacterByName(data.avatar_id);
                    if (data.companion_ids != null && data.companion_ids.Length > 0)
                        PlayerDataManager.Instance.SetSelectedCompanionByName(data.companion_ids[0]);

                    if (LevelLoader.Instance != null)
                        LevelLoader.Instance.LoadLevel("MapTest2");
                    else
                        GameLog.Warn("[UILogin] LevelLoader.Instance null, không LoadLevel được");
                }
                else
                {
                    onboarding.StartOnboarding();
                }
            },
            onError: err =>
            {
                GameLog.Warn("[UILogin] GET /me lỗi (coi như player mới): " + err);
                onboarding.StartOnboarding();
            });
    }

}