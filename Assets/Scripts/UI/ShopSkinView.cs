using UnityEngine;
using UnityEngine.UI;

public class ShopSkinItemView : MonoBehaviour
{
    [Header("UI")]
    public Image previewImage;
    public Text nameText;
    public Image currencyIcon;
    public Text priceText;
    public Text stateText;
    public Button actionButton;
    public Text actionButtonText;

    [Header("Icons")]
    public Sprite pointIcon;

    private string _skinId;
    private System.Action<string> _onClick;

    public void Bind(
        string skinId,
        string displayName,
        Sprite preview,
        int price,
        bool owned,
        bool selected,
        System.Action<string> onClick)
    {
        _skinId = skinId;
        _onClick = onClick;

        if (previewImage) previewImage.sprite = preview;
        if (nameText) nameText.text = displayName;

        if (currencyIcon)
            currencyIcon.sprite = pointIcon;

        if (priceText) priceText.text = price.ToString();

        // 상태/버튼
        if (owned)
        {
            if (selected)
            {
                if (stateText) stateText.text = "적용중";
                if (actionButtonText) actionButtonText.text = "적용중";
                if (actionButton) actionButton.interactable = false;
            }
            else
            {
                if (stateText) stateText.text = "보유중";
                if (actionButtonText) actionButtonText.text = "적용하기";
                if (actionButton) actionButton.interactable = true;
            }
        }
        else
        {
            if (stateText) stateText.text = "미보유";
            if (actionButtonText) actionButtonText.text = "구매하기";
            if (actionButton) actionButton.interactable = true;
        }

        if (actionButton)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() => _onClick?.Invoke(_skinId));
        }
    }
}
