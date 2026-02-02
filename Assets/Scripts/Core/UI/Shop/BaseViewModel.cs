using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

public class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected void RaisePropertyChanged(string propName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}

public class MainMenuViewModel : BaseViewModel
{
    public event PropertyChangedEventHandler PropertyChangedd; //new

    private bool _isSettingsVisible;
    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        set 
        {
            if (_isSettingsVisible == value) return; //new
            _isSettingsVisible = value;
            PropertyChangedd?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSettingsVisible)));
                
            /*        _isSettingsVisible = value; 
                    RaisePropertyChanged(nameof(IsSettingsVisible)); */
        }
    }

    public void OnSettingsClicked()
    {
        IsSettingsVisible = true;
    }

    /* public void OnPlayClicked() 
     {
         //UIManager.Instance.ShowScreen(gameObject.);
     }*/

    /* public void OnQuitClicked() 
  { 
      Application.Quit();  
  }*/


    #region NPC Seller
    [Header("NPC seller")]

    private bool _pendingDialogue;
    public bool PendingDialogue
    {
        get => _pendingDialogue;
        set
        {
            if (_pendingDialogue == value) return;
            _pendingDialogue = value;
            PropertyChangedd?.Invoke(this, new PropertyChangedEventArgs(nameof(PendingDialogue)));
        }
    }

    private bool _isDialogueVisible;
    public bool IsDialogueVisible
    {
        get => _isDialogueVisible;
        set
        {
            if (_isDialogueVisible == value) return;
            _isDialogueVisible = value;
            PropertyChangedd?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDialogueVisible)));
        }
    }

    public string CurrentNPCName { get; set; }

    public void OnTalkClicked()
    {
        IsDialogueVisible = true;
        //   PendingDialogue = false;
        //IsChatVisible = true;
    }

    public void OnSkipClicked()
    {
        IsDialogueVisible = false;
    }
    #endregion

    #region Shop

    [Header("Shop System")]

    private bool _isShopVisible;
    public bool IsShopVisible
    {
        get => _isShopVisible;
        set
        {
            if (_isShopVisible == value) return;
            _isShopVisible = value;
            PropertyChangedd?.Invoke(this, new PropertyChangedEventArgs(nameof(IsShopVisible)));
        }
    }

    private ShopData _currentShopData;
    public ShopData CurrentShopData
    {
        get => _currentShopData;
        set => _currentShopData = value;
    }

    // Shop methods
    public void OnShopClicked()
    {
        IsShopVisible = true;
    }

    public void OnCloseShopClicked()
    {
        IsShopVisible = false;
    }

    public void OnBuyItemClicked(string itemID)
    {
        if (CurrentShopData != null && CurrentShopData.HasItem(itemID))
        {
            var item = CurrentShopData.GetItem(itemID);
            // Logic mua hàng sẽ được xử lý ở đây
            Debug.Log($"Buying {item.itemName} for {item.price} coins");
        }
    }
    #endregion

    /*#region Authentication
    private bool _isAuthenticationVisible;
    public bool IsAuthenticationVisible
    {
        get => _isAuthenticationVisible;
        set
        {
            if (_isAuthenticationVisible == value) return;
            _isAuthenticationVisible = value;
            PropertyChangedd?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAuthenticationVisible)));
        }
    }

    private bool UserLoggedIn;
    public bool IsUserLoggedInn
    {
        get => _isUserLoggedIn;
        set
        {
            if (_isUserLoggedIn == value) return;
            _isUserLoggedIn = value;
            PropertyChangedd?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUserLoggedIn)));
        }
    }

    public void ShowAuthentication()
    {
        IsAuthenticationVisible = true;
    }

    public void HideAuthentication()
    {
        IsAuthenticationVisible = false;
    }
    #endregion*/

    #region Chat System
    private bool _isChatVisible;
    public bool IsChatVisible
    {
        get => _isChatVisible;
        set
        {
            if (_isChatVisible == value) return;
            _isChatVisible = value;
            PropertyChangedd?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChatVisible)));
        }
    }

    #endregion



    [Serializable]
    public class APIProductResponse
    {
        public int totalCount;
        public List<APIProductItem> items;
    }

    [Serializable]
    public class APIAttributeGroup
    {
        public string id;
        public int customId;
        public string name;
        public List<APIAttribute> attributes;
    }

    [Serializable]
    public class APIAttribute
    {
        public string id;
        public int customId;
        public string name;
        public string value; // null trong trường hợp size giày
    }

    [Serializable]
    public class APIProductItem
    {
        public string id;
        [SerializeField] public string title;
        public string imageUrl;
        public string itemUrl;
        public string customId;
        public string selectSize;
        public float price;
        public float regularPrice;
        public bool isPriceImpact;
        public int totalReviews;
        public float reviewStatFiveScale;
        [SerializeField] public string brandName;
        public List<APIImage> images;

        /*  // THÊM: Method để clean Unicode characters nếu cần
          public string GetSafeTitle()
          {
              return !string.IsNullOrEmpty(title) ?
                  System.Text.RegularExpressions.Regex.Replace(title, @"[^\u0000-\u007F]", "") :
                  "Unknown Item";
          }

          // THÊM: Method để lấy Unicode-safe brand name
          public string GetSafeBrandName()
          {
              return !string.IsNullOrEmpty(brandName) ?
                  System.Text.RegularExpressions.Regex.Replace(brandName, @"[^\u0000-\u007F]", "") :
                  "Unknown Brand";
          }*/

        public List<APIAttributeGroup> attributeGroups;
        public List<ProductVariant> variants;
    }



    [Serializable]
    public class APIImage
    {
        public string small;
        public string medium;
        public string origin;
    }
}
