// ProductDetailData.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using static MainMenuViewModel;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

/*[Serializable]
public class ProductDetailData
{
    public string id;
    public string title;
    public string imageUrl;
    public float price;
    public float regularPrice;
    public bool isPriceImpact;
    public int totalReviews;
    public float reviewStatFiveScale;
    public string brandName;
    public List<AttributeGroup> attributeGroups;
    public List<APIImage> images;

    // Convert từ APIProductItem
    public static ProductDetailData FromAPIProduct(APIProductItem apiItem)
    {
        return new ProductDetailData
        {
            id = apiItem.id,
            title = apiItem.title,
            imageUrl = apiItem.imageUrl,
            price = apiItem.price,
            regularPrice = apiItem.regularPrice,
            isPriceImpact = apiItem.isPriceImpact,
            totalReviews = apiItem.totalReviews,
            reviewStatFiveScale = apiItem.reviewStatFiveScale,
            brandName = apiItem.brandName,
            images = apiItem.images ?? new List<APIImage>()
        };
    }
}*/



[Serializable]
public class ProductDetailData
{
    public string id;  // Product ID chung
    public string customId;  // Custom ID từ backend
    public string title;
    public string imageUrl;
    public float price;
    public float regularPrice;
    public bool isPriceImpact;
    public int totalReviews;
    public float reviewStatFiveScale;
    public string brandName;
    public List<AttributeGroup> attributeGroups;
    public List<ProductVariant> variants;  // THÊM DÒNG NÀY
    public List<APIImage> images;

    // Convert từ APIProductItem
    public static ProductDetailData FromAPIProduct(APIProductItem apiItem)
    {
        return new ProductDetailData
        {
            id = apiItem.id,
            customId = apiItem.customId,
            title = apiItem.title,
            imageUrl = apiItem.imageUrl,
            price = apiItem.price,
            regularPrice = apiItem.regularPrice,
            isPriceImpact = apiItem.isPriceImpact,
            totalReviews = apiItem.totalReviews,
            reviewStatFiveScale = apiItem.reviewStatFiveScale,
            brandName = apiItem.brandName,
            images = apiItem.images ?? new List<APIImage>(),
            variants = apiItem.variants ?? new List<ProductVariant>()  // THÊM
        };
    }
}




[Serializable]
public class AttributeGroup
{
    public string id;
    public int customId;
    public string name;
    public List<Attribute> attributes;
}

[Serializable]
public class Attribute
{
    public string id;
    public int customId;
    public string name;
    public string value;
}


[Serializable]
public class ProductVariant
{
    public string id; // Variant ID
    public string customId;
    public float price;
    public float regularPrice;
    public string title;
    public string tagName;
    public string tagColor;
    public List<AttributeGroup> attributeGroups; // ✅ Thêm thuộc tính này
}

[Serializable]
public class VariantAttribute
{
    public string name;   // "Size 42", "Size 43"...
    public string value;  // "42", "43"...
}
