using UnityEngine;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    [Header("Menu Controls")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private GameObject settingsPanel;

    private MainMenuViewModel viewModel;

    public void Initialize(MainMenuViewModel viewModel)
    {
        this.viewModel = viewModel;
        SetupEventListeners();
    }

    private void SetupEventListeners()
    {
        settingsButton.onClick.AddListener(viewModel.OnSettingsClicked);
        // Uncomment khi cần
        // playButton.onClick.AddListener(viewModel.OnPlayClicked);
        // quitButton.onClick.AddListener(viewModel.OnQuitClicked);
    }

    public void OnViewModelChanged(string propertyName)
    {
        switch (propertyName)
        {
            case nameof(viewModel.IsSettingsVisible):
                settingsPanel.SetActive(viewModel.IsSettingsVisible);
                break;
        }
    }
}
