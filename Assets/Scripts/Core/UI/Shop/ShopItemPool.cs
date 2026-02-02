/*using System.Collections.Generic;
using UnityEngine;

public class ShopItemPool : MonoBehaviour
{
    public static ShopItemPool Instance { get; private set; }

    [SerializeField] private GameObject shopItemPrefab;
    [SerializeField] private int initialPoolSize = 10;

    private Queue<GameObject> availableItems = new Queue<GameObject>();
    private List<GameObject> usedItems = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        InitializePool();
    }

    public void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            var item = Instantiate(shopItemPrefab);
            item.SetActive(false);
            availableItems.Enqueue(item);
        }
    }

    public GameObject GetShopItem(Transform parent)
    {
        GameObject item;

        if (availableItems.Count > 0)
        {
            item = availableItems.Dequeue();
        }
        else
        {
            item = Instantiate(shopItemPrefab);
        }

        item.transform.SetParent(parent, false);
        item.SetActive(true);
        usedItems.Add(item);
        return item;
    }

    public void ReturnShopItem(GameObject item)
    {
        if (usedItems.Contains(item))
        {
            usedItems.Remove(item);
            item.SetActive(false);
            item.transform.SetParent(transform, false);
            availableItems.Enqueue(item);
        }
    }

    public void ReturnAllItems()
    {
        while (usedItems.Count > 0)
        {
            ReturnShopItem(usedItems[0]);
        }
    }
}

*/