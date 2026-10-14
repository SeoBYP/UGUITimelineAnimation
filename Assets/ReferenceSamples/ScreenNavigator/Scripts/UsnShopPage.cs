using SeoBYP.TimelineAnimation;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shop page: three tabs over a sheet container (REGULAR / SPECIAL / SALE), one item each.</summary>
public sealed class UsnShopPage : MonoBehaviour, IUIAnimScreenLifecycle
{
    public UIAnimSheetContainer sheets;
    public UIAnimScreen itemSetSheet;
    public Button[] tabs;
    public Button back;
    bool _ready;

    void Awake()
    {
        back.onClick.AddListener(() => UsnDemo.Instance.Back());
        for (var i = 0; i < tabs.Length; i++)
        {
            var index = sheets.Register(itemSetSheet);
            sheets.Sheets[index].GetComponent<UsnShopItemSet>().Show(index);
            tabs[i].onClick.AddListener(() => Select(index));
        }
    }

    async void Select(int index)
    {
        if (UIAnimScreenContainer.AnyInTransition) return;
        await sheets.Show(index);
    }

    public async Awaitable WillEnter(UIAnimTransition t)
    {
        if (_ready) return;
        _ready = true;
        await sheets.Show(0, animated: false);
    }
}
