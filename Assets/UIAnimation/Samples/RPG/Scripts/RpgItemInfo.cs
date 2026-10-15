using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Item info popup with a shared element: the icon grows out of the tapped slot ("Enter") and goes back into it
/// ("Exit"); only the slot's end of each is filled in here.
/// </summary>
public sealed class RpgItemInfo : MonoBehaviour, IUIAnimScreenLifecycle
{
    public Image icon, frame;
    public TMP_Text itemName, rarity, description, count;
    public Button close, use;

    Image _slotIcon;

    void Awake()
    {
        close.onClick.AddListener(() => RpgDemo.Instance.Back());
        use.onClick.AddListener(() => RpgDemo.Instance.Back());
    }

    public Awaitable WillEnter(UIAnimTransition t)
    {
        var demo = RpgDemo.Instance;
        var item = demo.SelectedItem;
        _slotIcon = demo.SelectedIcon.GetComponent<Image>();
        icon.sprite = _slotIcon.sprite;
        var color = RpgData.RarityColor(item.rarity);
        frame.color = color;
        itemName.text = item.name;
        rarity.text = item.rarity.ToString().ToUpperInvariant();
        rarity.color = color;
        description.text = item.text;
        count.text = "Owned: " + item.count;
        UIAnimSharedElement.SetPartnerKeys(GetComponent<UIAnimation>(), "Enter", icon.rectTransform, demo.SelectedIcon, atStart: true);
        _slotIcon.enabled = false;
        return UIAnimScreen.Completed();
    }

    public Awaitable WillExit(UIAnimTransition t)
    {
        UIAnimSharedElement.SetPartnerKeys(GetComponent<UIAnimation>(), "Exit", icon.rectTransform, _slotIcon.rectTransform, atStart: false);
        return UIAnimScreen.Completed();
    }

    public void DidExit(UIAnimTransition t) => _slotIcon.enabled = true;
}
