using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A letter in the mailbox: sender, title, the attached reward and CLAIM.</summary>
public sealed class RpgMailRow : MonoBehaviour
{
    public TMP_Text from, title, amount;
    public Image rewardIcon;
    public Button claim;

    public void Set(RpgData.Mail m)
    {
        from.text = m.from;
        title.text = m.title;
        rewardIcon.sprite = RpgDemo.Instance.Sprite(m.icon);
        amount.text = (m.gold + m.gems + m.stamina).ToString("N0");
    }
}
