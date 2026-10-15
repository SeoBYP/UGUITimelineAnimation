using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The lobby: player info and currencies on top, the main hero breathing (a looping state played on enable), an event banner that turns by
/// itself (scroll list: loop + one per swipe + auto scroll, with dots), the menu to the other screens, and the side buttons (shop, mail, attendance).
/// </summary>
public sealed class RpgLobby : MonoBehaviour
{
    public Button heroes, bag, summon, adventure, quests, shop, mail, attendance;
    public UIAnimScrollList banners;

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
        banners.SetData(Events, (RpgRow row, (string title, string text, string icon) e, int _) =>
        {
            row.title.text = e.title;
            row.text.text = e.text;
            row.icon.sprite = demo.Sprite(e.icon);
        });
    }
}
