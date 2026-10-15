using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A quest: title, progress bar, reward, and CLAIM (complete) or GO.</summary>
public sealed class RpgQuestRow : MonoBehaviour
{
    public TMP_Text title, progress, reward, buttonLabel;
    public RectTransform fill;
    public Image rewardIcon;
    public Button claim;

    public void Set(RpgData.Quest q)
    {
        title.text = q.title;
        progress.text = $"{q.progress:N0}/{q.goal:N0}";
        var max = fill.anchorMax;
        max.x = Mathf.Clamp01((float)q.progress / q.goal);
        fill.anchorMax = max;
        rewardIcon.sprite = RpgDemo.Instance.Sprite(q.rewardIcon);
        reward.text = q.reward.ToString("N0");
        claim.interactable = q.Complete;
        buttonLabel.text = q.Complete ? "CLAIM" : "GO";
    }
}
