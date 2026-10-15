using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A hero's page. The portrait is a shared element: it grows out of the tapped card in the heroes grid ("PushEnter")
/// and shrinks back into it ("PopExit"). The info list has section headers that stick to the top, and the title area
/// collapses as the list scrolls (UIAnimScrollScrub, state "Collapse").
/// </summary>
public sealed class RpgHeroDetail : MonoBehaviour, IUIAnimScreenLifecycle
{
    public Image portrait, weapon;
    public TMP_Text heroName, role, level;
    public GameObject[] stars;
    public UIAnimScrollList list;

    RectTransform _card;
    RpgData.Hero _hero;

    // Rows: STATS header, 4 stats, SKILLS header, 3 skills, STORY header, story.
    const int StatsStart = 1, SkillsHeader = 5, SkillsStart = 6, StoryHeader = 9, Story = 10, Rows = 11;
    const int StatPrefab = 0, SkillPrefab = 1, HeaderPrefab = 2, StoryPrefab = 3;

    static int PrefabOf(int i) => i is 0 or SkillsHeader or StoryHeader ? HeaderPrefab : i < SkillsHeader ? StatPrefab : i < StoryHeader ? SkillPrefab : StoryPrefab;

    void Awake()
    {
        list.prefabOf = PrefabOf;
        list.sizeOf = i => PrefabOf(i) switch { HeaderPrefab => 70f, StatPrefab => 92f, SkillPrefab => 120f, _ => 200f };
    }

    public Awaitable WillEnter(UIAnimTransition t)
    {
        if (t.Kind != UIAnimTransitionKind.Push) return UIAnimScreen.Completed();
        var demo = RpgDemo.Instance;
        _hero = RpgData.Heroes[demo.SelectedHero];
        _card = demo.SelectedPortrait;
        portrait.sprite = demo.Sprite(_hero.id);
        weapon.sprite = demo.Sprite(_hero.weapon);
        heroName.text = _hero.name;
        role.text = _hero.role.ToUpperInvariant();
        level.text = "Lv. " + _hero.level;
        for (var i = 0; i < stars.Length; i++) stars[i].SetActive(i < _hero.stars);
        list.SetCount(Rows, (RpgRow row, int i) => Bind(row, i));

        UIAnimSharedElement.SetPartnerKeys(GetComponent<UIAnimation>(), "PushEnter", portrait.rectTransform, _card, atStart: true);
        _card.GetComponent<Image>().enabled = false;   // the portrait leaves the card…
        return UIAnimScreen.Completed();
    }

    public Awaitable WillExit(UIAnimTransition t)
    {
        if (t.Kind == UIAnimTransitionKind.Pop)
            UIAnimSharedElement.SetPartnerKeys(GetComponent<UIAnimation>(), "PopExit", portrait.rectTransform, _card, atStart: false);
        return UIAnimScreen.Completed();
    }

    public void DidExit(UIAnimTransition t)
    {
        if (t.Kind == UIAnimTransitionKind.Pop) _card.GetComponent<Image>().enabled = true;   // …and lands back in it
    }

    void Bind(RpgRow row, int i)
    {
        var demo = RpgDemo.Instance;
        if (i == 0 || i == SkillsHeader || i == StoryHeader)
        {
            row.title.text = i == 0 ? "STATS" : i == SkillsHeader ? "SKILLS" : "STORY";
        }
        else if (i < SkillsHeader)
        {
            var s = i - StatsStart;
            var value = RpgData.Stat(_hero, s);
            row.icon.sprite = demo.Sprite(RpgData.StatIcons[s]);
            row.title.text = RpgData.Stats[s].name;
            row.value.text = value.ToString("N0");
            row.SetFill((float)value / RpgData.Stats[s].max);
        }
        else if (i < StoryHeader)
        {
            var (icon, skillName, text) = _hero.skills[i - SkillsStart];
            row.icon.sprite = demo.Sprite(icon);
            row.title.text = skillName;
            row.text.text = text;
        }
        else row.text.text = _hero.story;
    }
}
