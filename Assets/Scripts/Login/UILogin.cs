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
        onboarding.StartOnboarding();
    }
}