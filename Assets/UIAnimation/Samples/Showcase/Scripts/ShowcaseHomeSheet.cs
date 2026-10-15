using UnityEngine;
using UnityEngine.UI;

/// <summary>Home tab: opens the reward and level-up modals, and shows a toast.</summary>
public sealed class ShowcaseHomeSheet : MonoBehaviour
{
    public Button reward, levelUp, toast;

    void Awake()
    {
        reward.onClick.AddListener(() => ShowcaseDemo.Instance.ShowReward());
        levelUp.onClick.AddListener(() => ShowcaseDemo.Instance.ShowLevelUp());
        toast.onClick.AddListener(() => GetComponentInParent<ShowcaseLobby>().Toast("Saved!"));
    }
}
