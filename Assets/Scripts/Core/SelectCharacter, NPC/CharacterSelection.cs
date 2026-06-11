using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelection : MonoBehaviour
{
    [Header("Character Data")]
    public CharacterData[] characterDataArray; // Array CharacterData thay vì GameObject[]

    [Header("Character Display")]
    public Transform[] characterPositions; // Vị trí trái(0), giữa(1), phải(2)

    [Header("UI Elements")]
    public Button createCharacterButton;
    public TextMeshProUGUI characterNameText;
    public TextMeshProUGUI characterDetailText;

    private GameObject[] instantiatedCharacters;
    private int currentCharacterIndex = 1;
    private Vector2 startTouchPosition;
    private float swipeThreshold = 50f;

    public GameObject companionSelectionPanel;
    public GameObject playerCharacterPanel;


    void Start()
    {
        PlayerDataManager.Instance.RegisterCharacterData(characterDataArray);
        InitializeCharacters();
        UpdateCharacterPositions();
        createCharacterButton.onClick.AddListener(OnCreateCharacterButtonClicked);
    }

    void InitializeCharacters()
    {
        instantiatedCharacters = new GameObject[2];

        for (int i = 0; i < instantiatedCharacters.Length && i < characterDataArray.Length; i++)
        {
            GameObject previewPrefab = characterDataArray[i].previewPrefab;
            instantiatedCharacters[i] = Instantiate(previewPrefab);
            instantiatedCharacters[i].transform.localPosition = Vector3.zero;
        }

        currentCharacterIndex = 0;

        ApplyLayoutForCurrentIndex();
        UpdateCharacterInfo();
    }

    private void OnDestroy()
    {
        CleanupCharacters();
    }

    private void CleanupCharacters()
    {
        if (instantiatedCharacters == null) return;

        foreach (var character in instantiatedCharacters)
        {
            if (character != null)
                Destroy(character);
        }
        instantiatedCharacters = null;
    }


    void Update()
    {
        HandleSwipeInput();
    }

    void HandleSwipeInput()
    {
        if (Input.touchCount > 0 && playerCharacterPanel.activeInHierarchy)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                startTouchPosition = touch.position;
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                Vector2 endTouchPosition = touch.position;
                Vector2 swipeDirection = endTouchPosition - startTouchPosition;

                if (Mathf.Abs(swipeDirection.x) > swipeThreshold)
                {
                    if (swipeDirection.x > 0)
                        SwitchToPreviousCharacter();
                    else
                        SwitchToNextCharacter();
                }
            }
        }
    }

    void SwitchToNextCharacter()
    {
        if (currentCharacterIndex >= characterDataArray.Length - 1)
            return;

        AudioManager.Instance.PlaySFXOneShot("Swipe");

        currentCharacterIndex++;
        ApplyLayoutForCurrentIndex();
        UpdateCharacterInfo();
    }

    void SwitchToPreviousCharacter()
    {
        if (currentCharacterIndex <= 0)
            return;

        AudioManager.Instance.PlaySFXOneShot("Swipe");

        currentCharacterIndex--;
        ApplyLayoutForCurrentIndex();
        UpdateCharacterInfo();
    }


    void ApplyLayoutForCurrentIndex()
    {
        if (instantiatedCharacters == null || instantiatedCharacters.Length < 2)
            return;

        GameObject male = instantiatedCharacters[0];
        GameObject female = instantiatedCharacters[1];

        for (int i = 0; i < characterPositions.Length; i++)
        {
            foreach (Transform child in characterPositions[i])
                child.SetParent(null);
        }

        if (currentCharacterIndex == 1)
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

    void UpdateCharacterPositions()
    {
        for (int i = 0; i < characterPositions.Length; i++)
        {
            foreach (Transform child in characterPositions[i])
            {
                bool isCenter = (i == 1);

                child.localScale = isCenter ? Vector3.one : Vector3.one * 0.8f;
                SetCharacterLayerOrder(child.gameObject, isCenter ? 10 : 5);
                SetCharacterText(child.gameObject, isCenter);
                SetCharacterBrightness(child.gameObject, isCenter);
            }
        }
    }

    void SetCharacterText(GameObject character, bool isCenter)
    {
        TextMeshProUGUI[] tmpTexts = character.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var tmp in tmpTexts)
            tmp.gameObject.SetActive(isCenter);

        Text[] uiTexts = character.GetComponentsInChildren<Text>(true);
        foreach (var txt in uiTexts)
            txt.gameObject.SetActive(isCenter);
    }

    void SetCharacterBrightness(GameObject character, bool isCenter)
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
                if (i == 1)
                    c = Color.Lerp(c, new Color(0.5f, 0.5f, 0.5f, 0f), 1f);
                else
                    c = Color.Lerp(c, new Color(0.5f, 0.5f, 0.5f, 1f), 1f);
            }

            img.color = c;
        }
    }

    void SetCharacterLayerOrder(GameObject character, int order)
    {
        Renderer[] renderers = character.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
            renderer.sortingOrder = order;
    }

    void UpdateCharacterInfo()
    {
        if (characterNameText != null && currentCharacterIndex < characterDataArray.Length)
            characterNameText.text = characterDataArray[currentCharacterIndex].characterName;

        if (characterDetailText != null)
        {
            // description giờ là string[] — lấy phần tử [0] với null/empty guard
            var desc = characterDataArray[currentCharacterIndex].description;
            characterDetailText.text = (desc != null && desc.Length > 0) ? desc[0] : "";
        }
        UpdateCharacterPositions();
    }

    void OnCreateCharacterButtonClicked()
    {
        PlayerDataManager.Instance.SaveCharacterIndex(currentCharacterIndex);

        Debug.Log($"[CharacterSelection] Player đã chọn character: {characterDataArray[currentCharacterIndex].characterName}");

        TransitionToCompanionSelection();
    }

    void TransitionToCompanionSelection()
    {
        if (playerCharacterPanel != null)
            playerCharacterPanel.SetActive(false);

        if (companionSelectionPanel != null)
            companionSelectionPanel.SetActive(true);

        Debug.Log("[CharacterSelection] Chuyển sang Companion Selection");
    }
}
