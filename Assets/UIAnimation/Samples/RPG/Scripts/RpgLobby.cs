using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The lobby: player info and currencies on top, the main hero breathing (a looping state played on enable), an event banner that turns by
/// itself (scroll list: loop + one per swipe + auto scroll, with dots), the menu to the other screens, and the side buttons (shop, mail, attendance; guild, ranking, story; settings).
/// </summary>
public sealed class RpgLobby : MonoBehaviour
{
    public Button heroes, bag, summon, adventure, quests, shop, mail, attendance, guild, ranking, story, settings;
    public UIAnimScrollList banners;
    [Tooltip("Badges: the number of letters, and \"!\" while today's attendance is unclaimed.")]
    public TMP_Text mailBadge;
    public GameObject attendanceBadge;

    static readonly (string title, string text, string icon)[] Events =
    {
        ("SUMMON FESTIVAL", "Legendary rate x2 this week", "menu_summon"),
        ("DRAGON RAID", "Defeat the dragon for a Royal Seal", "ch_dragon"),
        ("DAILY LOGIN", "Day 5: 300 gems", "chest"),
    };

    void Awake()
    {
        var demo = RpgDemo.Instance;
        heroes.onClick.AddListener(() => demo.Open(demo.heroes));
        bag.onClick.AddListener(() => demo.Open(demo.bag));
        summon.onClick.AddListener(() => demo.Open(demo.summon));
        adventure.onClick.AddListener(() => demo.Open(demo.adventure));
        quests.onClick.AddListener(() => demo.Open(demo.quests));
        shop.onClick.AddListener(() => demo.Open(demo.shop));
        mail.onClick.AddListener(() => demo.Open(demo.mail));
        attendance.onClick.AddListener(demo.OpenAttendance);
        guild.onClick.AddListener(() => demo.Open(demo.guild));
        ranking.onClick.AddListener(() => demo.Open(demo.ranking));
        story.onClick.AddListener(() => demo.Open(demo.story));
        settings.onClick.AddListener(demo.OpenSettings);
        banners.SetData(Events, (RpgRow row, (string title, string text, string icon) e, int _) =>
        {
            row.title.text = e.title;
            row.text.text = e.text;
            row.icon.sprite = demo.Sprite(e.icon);
        });
    }

    // Shown again (a page popped) or paid out under a popup (attendance, mail): the badges follow.
    void OnEnable()
    {
        RpgDemo.Instance.CurrencyChanged += Refresh;
        Refresh();
    }

    void OnDisable() => RpgDemo.Instance.CurrencyChanged -= Refresh;

    public void Refresh()
    {
        var demo = RpgDemo.Instance;
        var letters = demo.Mails.Count;
        mailBadge.text = letters.ToString();
        mailBadge.transform.parent.gameObject.SetActive(letters > 0);
        attendanceBadge.SetActive(!demo.AttendanceClaimed);
    }
}
