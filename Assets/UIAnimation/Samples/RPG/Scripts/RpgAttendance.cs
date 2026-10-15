using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attendance popup: seven days, the claimed ones stamped, today's glowing (a looping state). CLAIM stamps today
/// ("Stamp" on the day: the stamp slams down, the cell shakes) and pays the reward; the 7th day is the chest.
/// </summary>
public sealed class RpgAttendance : MonoBehaviour, IUIAnimScreenLifecycle
{
    public UIAnimation[] days;
    public Image[] rewardIcons;
    public TMP_Text[] amounts;
    public Button claim, close;

    void Awake()
    {
        close.onClick.AddListener(() => RpgDemo.Instance.Back());
        claim.onClick.AddListener(Claim);
    }

    public Awaitable WillEnter(UIAnimTransition t)
    {
        var demo = RpgDemo.Instance;
        for (var i = 0; i < days.Length; i++)
        {
            var (icon, amount) = RpgData.Attendance[i];
            rewardIcons[i].sprite = demo.Sprite(icon);
            amounts[i].text = icon == "chest" ? "x1" : amount.ToString("N0");
            days[i].Sample("Stamp", i < demo.AttendanceDay ? 1f : 0f);
            days[i].Stop("Today");
            days[i].Sample("Today", 0f);
        }
        claim.interactable = !demo.AttendanceClaimed;
        if (!demo.AttendanceClaimed) days[demo.AttendanceDay].Play("Today");
        return UIAnimScreen.Completed();
    }

    async void Claim()
    {
        var demo = RpgDemo.Instance;
        if (demo.AttendanceClaimed) return;
        var today = days[demo.AttendanceDay];
        claim.interactable = false;
        today.Stop("Today");
        today.Sample("Today", 0f);
        await today.PlayAsync("Stamp");
        demo.ClaimAttendance();
    }
}
