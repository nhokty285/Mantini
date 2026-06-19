using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelection : MonoBehaviour
{
    [Header("Character Data")]
    public CharacterData[] characterDataArray;

    [Header("Character Display")]
    public Transform[] characterPositions; // 0=trái, 1=giữa, 2=phải

    [Header("UI Elements")]
    public Button createCharacterButton;
    public TextMeshProUGUI characterNameText;
    public TextMeshProUGUI characterDetailText;

    public GameObject companionSelectionPanel;
    public GameObject playerCharacterPanel;

    private GameObject[] _instantiatedCharacters;
    private int _currentCharacterIndex = 1;
    private Vector2 _startTouchPosition;
    private const float SwipeThreshold = 50f;

    private void Start()
    {
        PlayerDataManager.Instance.RegisterCharacterData(characterDataArray);
        InitializeCharacters();
        UpdateCharacterPositions();

        if (createCharacterButton != null)
        {
            // Mantini convention: RemoveAllListeners trước AddListener
            createCharacterButton.onClick.RemoveAllListeners();
            createCharacterButton.onClick.AddListener(OnCreateCharacterButtonClicked);
        }
    }

    private void InitializeCharacters()
    {
        _instantiatedCharacters = new GameObject[2];

        for (int i = 0; i < _instantiatedCharacters.Length && i < characterDataArray.Length; i++)
        {
            GameObject previewPrefab = characterDataArray[i].previewPrefab;
            _instantiatedCharacters[i] = Instantiate(previewPrefab);
            _instantiatedCharacters[i].transform.localPosition = Vector3.zero;
        }

        _currentCharacterIndex = 0;

        ApplyLayoutForCurrentIndex();
        UpdateCharacterInfo();
    }

    private void OnDestroy() => CleanupCharacters();

    private void CleanupCharacters()
    {
        if (_instantiatedCharacters == null) return;

        foreach (var character in _instantiatedCharacters)
        {
            if (character != null) Destroy(character);
        }
        _instantiatedCharacters = null;
    }

    private void Update() => HandleSwipeInput();

    private void HandleSwipeInput()
    {
        if (Input.touchCount <= 0) return;
        if (playerCharacterPanel == null || !playerCharacterPanel.activeInHierarchy) return;

        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            _startTouchPosition = touch.position;
        }
        else if (touch.phase == TouchPhase.Ended)
        {
            Vector2 endTouchPosition = touch.position;
            Vector2 swipeDirection = endTouchPosition - _startTouchPosition;

            if (Mathf.Abs(swipeDirection.x) > SwipeThreshold)
            {
                if (swipeDirection.x > 0) SwitchToPreviousCharacter();
                else SwitchToNextCharacter();
            }
        }
    }

    private void SwitchToNextCharacter()
    {
        if (_currentCharacterIndex >= characterDataArray.Length - 1) return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFXOneShot("Swipe");

        _currentCharacterIndex++;
        ApplyLayoutForCurrentIndex();
        UpdateCharacterInfo();
    }

    private void SwitchToPreviousCharacter()
    {
        if (_currentCharacterIndex <= 0) return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFXOneShot("Swipe");

        _currentCharacterIndex--;
        ApplyLayoutForCurrentIndex();
        UpdateCharacterInfo();
    }

    private void ApplyLayoutForCurrentIndex()
    {
        if (_instantiatedCharacters == null || _instantiatedCharacters.Length < 2) return;

        GameObject male = _instantiatedCharacters[0];
        GameObject female = _instantiatedCharacters[1];

        // Unparent all children dưới character positions
        for (int i = 0; i < characterPositions.Length; i++)
        {
            foreach (Transform child in characterPositions[i])
                child.SetParent(null);
        }

        if (_currentCharacterIndex == 1)
        {
            female.transform.SetParent(characterPositions[1], false);
            female.transform.localPosition = Vector3.zero;

            male.transform.SetParent(characterPositions[0], false);
            male.transform.localPosition = Vector3.zero;
        }
        else
        {
            male.transform.SetParent(characterPositions[1], false);
            male.transform.localPosition = Vector3.zero;

            female.transform.SetParent(characterPositions[2], false);
            female.transform.localPosition = Vector3.zero;
        }

        UpdateCharacterPositions();
    }

    private void UpdateCharacterPositions()
    {
        for (int i = 0; i < characterPositions.Length; i++)
        {
            bool isCenter = (i == 1);
            foreach (Transform child in characterPositions[i])
            {
                child.localScale = isCenter ? Vector3.one : Vector3.one * 0.8f;
                SetCharacterLayerOrder(child.gameObject, isCenter ? 10 : 5);
                SetCharacterText(child.gameObject, isCenter);
                SetCharacterBrightness(child.gameObject, isCenter);
            }
        }
    }

    private static void SetCharacterText(GameObject character, bool isCenter)
    {
        TextMeshProUGUI[] tmpTexts = character.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var tmp in tmpTexts) tmp.gameObject.SetActive(isCenter);

        Text[] uiTexts = character.GetComponentsInChildren<Text>(true);
        foreach (var txt in uiTexts) txt.gameObject.SetActive(isCenter);
    }

    private static void SetCharacterBrightness(GameObject character, bool isCenter)
    {
        RawImage[] imgs = character.GetComponentsInChildren<RawImage>(true);
        for (int i = 0; i < imgs.Length; i++)
        {
            var img = imgs[i];
            Color c = img.color;
            if (isCenter)
            {
                c.r = 1f; c.g = 1f; c.b = 1f; c.a = 1f;
            }
            else
            {
                // Slot 1: invisible side, slot khác: dim
                c = (i == 1)
                    ? Color.Lerp(c, new Color(0.5f, 0.5f, 0.5f, 0f), 1f)
                    : Color.Lerp(c, new Color(0.5f, 0.5f, 0.5f, 1f), 1f);
            }
            img.color = c;
        }
    }

    private static void SetCharacterLayerOrder(GameObject character, int order)
    {
        Renderer[] renderers = character.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers) renderer.sortingOrder = order;
    }

    private void UpdateCharacterInfo()
    {
        if (characterNameText != null && _currentCharacterIndex < characterDataArray.Length)
            characterNameText.text = characterDataArray[_currentCharacterIndex].characterName;

        if (characterDetailText != null)
        {
            var desc = characterDataArray[_currentCharacterIndex].description;
            characterDetailText.text = (desc != null && desc.Length > 0) ? desc[0] : "";
        }
        UpdateCharacterPositions();
    }

    private void OnCreateCharacterButtonClicked()
    {
        PlayerDataManager.Instance.SaveCharacterIndex(_currentCharacterIndex);
#if UNITY_EDITOR
        GameLog.Info($"[CharacterSelection] Player đã chọn character: {characterDataArray[_currentCharacterIndex].characterName}");
#endif
        TransitionToCompanionSelection();
    }

    private void TransitionToCompanionSelection()
    {
        if (playerCharacterPanel != null) playerCharacterPanel.SetActive(false);
        if (companionSelectionPanel != null) companionSelectionPanel.SetActive(true);
#if UNITY_EDITOR
        GameLog.Info("[CharacterSelection] Chuyển sang Companion Selection");
#endif
    }
}