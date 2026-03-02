using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerCharacterSelection : MonoBehaviour
{
    [Header("Character Data")]
    public CharacterData[] characterDataArray; // Array CharacterData thay vì GameObject[]

    [Header("Character Display")]
    public Transform[] characterPositions; // Vị trí trái(0), giữa(1), phải(2)

    [Header("UI Elements")]
    public Button createCharacterButton;
    public TextMeshProUGUI characterNameText;

    private GameObject[] instantiatedCharacters; // Preview instances
    private int currentCharacterIndex = 1;
    private Vector2 startTouchPosition;
    private float swipeThreshold = 50f;

    public GameObject companionSelectionPanel;
    public GameObject playerCharacterPanel;

    void Start()
    {
        // Đăng ký CharacterData vào PlayerDataManager
        PlayerDataManager.Instance.RegisterCharacterData(characterDataArray);

        InitializeCharacters();
        UpdateCharacterPositions();
        createCharacterButton.onClick.AddListener(OnCreateCharacterButtonClicked);
    }

    /*  void InitializeCharacters()
      {
          instantiatedCharacters = new GameObject[3];

          // Tạo 3 preview instances từ previewPrefab
          for (int i = 0; i < 3 && i < characterDataArray.Length; i++)
          {
              // Dùng previewPrefab (RawImage) cho selection screen
              GameObject previewPrefab = characterDataArray[i].previewPrefab;
              instantiatedCharacters[i] = Instantiate(previewPrefab, characterPositions[i]);
              instantiatedCharacters[i].transform.localPosition = Vector3.zero;
          }
      }
  */

    void InitializeCharacters()
    {
        // 2 nhân vật: 0 = Nam, 1 = Nữ (hoặc ngược lại tùy bạn setup trong Inspector)
        instantiatedCharacters = new GameObject[2];

        for (int i = 0; i < instantiatedCharacters.Length && i < characterDataArray.Length; i++)
        {
            GameObject previewPrefab = characterDataArray[i].previewPrefab;
            instantiatedCharacters[i] = Instantiate(previewPrefab);
            instantiatedCharacters[i].transform.localPosition = Vector3.zero;
        }

        // Trạng thái khởi đầu: ví dụ Nữ được chọn
        currentCharacterIndex = 0; // 1 = Nữ, 0 = Nam (tùy cách bạn đặt trong array)

        // Nữ ở giữa, Nam bên trái (giống hình 1)
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
                    {
                        SwitchToPreviousCharacter();
                    }
                    else
                    {
                        SwitchToNextCharacter();
                    }
                }
            }
        }
    }

    void SwitchToNextCharacter()
    {
        // Vuốt trái: chỉ cho đi tới nếu còn “next”
        if (currentCharacterIndex >= characterDataArray.Length - 1)
            return;

            AudioManager.Instance.PlaySFXOneShot("Swipe");
        
        currentCharacterIndex++;
        ApplyLayoutForCurrentIndex();
        UpdateCharacterInfo();
    }

    void SwitchToPreviousCharacter()
    {
        // Vuốt phải: chỉ cho lùi nếu còn “previous”
        if (currentCharacterIndex <= 0)
            return;
    
            AudioManager.Instance.PlaySFXOneShot("Swipe");
        
        currentCharacterIndex--;
        ApplyLayoutForCurrentIndex();
        UpdateCharacterInfo();
    }


    // currentCharacterIndex: 0 = Nam, 1 = Nữ (ví dụ)
    // characterPositions[0] = trái, [1] = giữa, [2] = phải
    void ApplyLayoutForCurrentIndex()
    {
        if (instantiatedCharacters == null || instantiatedCharacters.Length < 2)
            return;

        GameObject male = instantiatedCharacters[0];
        GameObject female = instantiatedCharacters[1];

        // Clear con cũ trong 3 vị trí
        for (int i = 0; i < characterPositions.Length; i++)
        {
            foreach (Transform child in characterPositions[i])
            {
                child.SetParent(null);
            }
        }

        if (currentCharacterIndex == 1)
        {
            // Trạng thái A: Nữ được chọn
            // Nữ ở giữa, Nam bên trái
            female.transform.SetParent(characterPositions[1], false);
            female.transform.localPosition = Vector3.zero;

            male.transform.SetParent(characterPositions[0], false);
            male.transform.localPosition = Vector3.zero;
        }
        else
        {
            // Trạng thái B: Nam được chọn
            // Nam ở giữa, Nữ bên phải
            male.transform.SetParent(characterPositions[1], false);
            male.transform.localPosition = Vector3.zero;

            female.transform.SetParent(characterPositions[2], false);
            female.transform.localPosition = Vector3.zero;
        }

        UpdateCharacterPositions(); // scale + layer
    }

    /* void RearrangeCharacters(bool moveRight)
     {
         GameObject[] tempCharacters = new GameObject[3];

         if (moveRight)
         {
             tempCharacters[0] = instantiatedCharacters[1];
             tempCharacters[1] = instantiatedCharacters[2];
             tempCharacters[2] = instantiatedCharacters[0];
         }
         else
         {
             tempCharacters[0] = instantiatedCharacters[2];
             tempCharacters[1] = instantiatedCharacters[0];
             tempCharacters[2] = instantiatedCharacters[1];
         }

         for (int i = 0; i < 3; i++)
         {
             tempCharacters[i].transform.SetParent(characterPositions[i]);
             tempCharacters[i].transform.localPosition = Vector3.zero;
         }

         instantiatedCharacters = tempCharacters;
     }*/

    /*  void UpdateCharacterPositions()
      {
          for (int i = 0; i < instantiatedCharacters.Length; i++)
          {
              if (i == 1) // Character ở giữa
              {
                  instantiatedCharacters[i].transform.localScale = Vector3.one;
                  SetCharacterLayerOrder(instantiatedCharacters[i], 10);
              }
              else // Characters ở 2 bên
              {
                  instantiatedCharacters[i].transform.localScale = Vector3.one * 0.8f;
                  SetCharacterLayerOrder(instantiatedCharacters[i], 5);
              }
          }
      }*/

    void UpdateCharacterPositions()
    {
        // Duyệt 3 vị trí
        for (int i = 0; i < characterPositions.Length; i++)
        {
            foreach (Transform child in characterPositions[i])
            {
                bool isCenter = (i == 1); // vị trí giữa

                child.localScale = isCenter ? Vector3.one : Vector3.one * 0.8f;
                SetCharacterLayerOrder(child.gameObject, isCenter ? 10 : 5);


                // Sáng nếu ở giữa, tối nếu ở 2 bên
                SetCharacterBrightness(child.gameObject, isCenter);

            }
        }
    }
    void SetCharacterBrightness(GameObject character, bool isCenter)
    {
        RawImage[] imgs = character.GetComponentsInChildren<RawImage>(true);
        foreach (var img in imgs)
        {
            Color c = img.color;

            if (isCenter)
            {
                // Màu gốc, sáng bình thường
                c.r = 1f;
                c.g = 1f;
                c.b = 1f;
                c.a = 1f;
            }
            else
            {
                // Ám xám: giảm saturation + hơi mờ
                // Ví dụ tint về xám nhạt
                c = Color.Lerp(c, new Color(0.5f, 0.5f, 0.5f, 1f),1f);
                //c.a = 0.7f;   // nhẹ mờ hơn trung tâm
            }

            img.color = c;
        }
    }
    void SetCharacterLayerOrder(GameObject character, int order)
    {
        Renderer[] renderers = character.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.sortingOrder = order;
        }
    }

    void UpdateCharacterInfo()
    {
        if (characterNameText != null && currentCharacterIndex < characterDataArray.Length)
        {
            characterNameText.text = characterDataArray[currentCharacterIndex].characterName;
        }

        UpdateCharacterPositions();
    }

    void OnCreateCharacterButtonClicked()
    {
        
        // Lưu index vào PlayerDataManager
        PlayerDataManager.Instance.SaveCharacterIndex(currentCharacterIndex);

        Debug.Log($"Player đã chọn character: {characterDataArray[currentCharacterIndex].characterName}");

        TransitionToCompanionSelection();
    }

    void TransitionToCompanionSelection()
    {
        if (playerCharacterPanel != null)
        {
            playerCharacterPanel.SetActive(false);
        }

        if (companionSelectionPanel != null)
        {
            companionSelectionPanel.SetActive(true);
        }

        Debug.Log("Chuyển sang Companion Selection");
    }
}
