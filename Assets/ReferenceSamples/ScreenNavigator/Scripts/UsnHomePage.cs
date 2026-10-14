using SeoBYP.TimelineAnimation;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Home page: the menu (feature flags lock three items), Back to Top, and a 1 s "preload" before it enters.</summary>
public sealed class UsnHomePage : MonoBehaviour, IUIAnimScreenLifecycle
{
    public Button back, shop, settings;
    [Tooltip("Menu buttons in order; their Locked / Unlocked children show the feature flag.")]
    public Button[] menu;
    public bool[] unlocked = { true, true, false, false, false };   // shop, settings, main quest, event quest, mission

    void Awake()
    {
        back.onClick.AddListener(() => UsnDemo.Instance.Back());
        shop.onClick.AddListener(() => UsnDemo.Instance.ShowShop());
        settings.onClick.AddListener(() => Debug.Log("[UsnDemo] Settings modal: comes with the modal screens (D3)."));
        for (var i = 0; i < menu.Length; i++)
        {
            menu[i].transform.Find("Locked").gameObject.SetActive(!unlocked[i]);
            menu[i].transform.Find("Unlocked").gameObject.SetActive(unlocked[i]);
        }
    }

    // The original preloads the shop and waits 1 s here; Loading stays visible meanwhile.
    public async Awaitable WillEnter(UIAnimTransition t)
    {
        if (t.Kind == UIAnimTransitionKind.Push) await Awaitable.WaitForSecondsAsync(1f);
    }
}
