using System;
using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battle HUD (opened from the formation). The team attacks by itself; each hit shakes the enemy ("Hit") and a damage
/// number pops up from it ("Pop" on a number from a small pool). Skills hit harder and then cool down (the radial
/// fill, "Cooldown"). The enemy's and the team's health bars ease toward their values. A won battle ends with the
/// enemy's "Die", a lost one with the page's "Lose"; then the result popup. GIVE UP asks first (the confirm popup).
/// </summary>
public sealed class RpgBattle : MonoBehaviour, IUIAnimScreenLifecycle
{
    public UIAnimation enemy;
    public TMP_Text enemyName;
    public RectTransform enemyHp;
    public Image[] teamPortraits;
    public RectTransform[] teamHp;
    public Button[] skills;
    public Image[] skillIcons;
    public TMP_Text[] numbers;
    public Button giveUp;
    [Min(0.1f)] public float attackSeconds = 0.5f;

    const int AutoDamage = 150, SkillDamage = 400;
    float _enemyMax, _enemy, _team = 1f;
    int _next;
    bool _over;

    public bool Over => _over;

    void Awake()
    {
        for (var i = 0; i < skills.Length; i++)
        {
            var skill = i;
            skills[i].onClick.AddListener(() => Cast(skill));
        }
        giveUp.onClick.AddListener(() => RpgDemo.Instance.Confirm("GIVE UP?", "The stamina is not returned.", () => End(false)));
    }

    public Awaitable WillEnter(UIAnimTransition t)
    {
        var demo = RpgDemo.Instance;
        var (chapter, stage) = demo.SelectedStage;
        var boss = !RpgDemo.Winnable(stage);
        enemyName.text = boss ? $"DRAGON  ·  {chapter + 1}-{stage + 1}" : $"FIRE WYRM  ·  {chapter + 1}-{stage + 1}";
        _enemyMax = _enemy = boss ? 99999 : 1000;
        for (var i = 0; i < teamPortraits.Length; i++)
        {
            var hero = i < demo.Team.Length ? demo.Team[i] : -1;
            teamPortraits[i].transform.parent.gameObject.SetActive(hero >= 0);
            if (hero >= 0) teamPortraits[i].sprite = demo.Sprite(RpgData.Heroes[hero].id);
        }
        var lead = RpgData.Heroes[demo.Team.Length > 0 ? demo.Team[0] : 0];
        for (var i = 0; i < skillIcons.Length; i++) skillIcons[i].sprite = demo.Sprite(lead.skills[i].icon);
        Bars(true);
        return UIAnimScreen.Completed();
    }

    public void DidEnter(UIAnimTransition t)
    {
        if (t.Kind == UIAnimTransitionKind.Push) Fight();
    }

    async void Fight()
    {
        var boss = !RpgDemo.Winnable(RpgDemo.Instance.SelectedStage.stage);
        while (!_over && this != null)
        {
            await Awaitable.WaitForSecondsAsync(attackSeconds);
            if (_over || this == null) return;
            Hit(AutoDamage + UnityEngine.Random.Range(-20, 21));
            if (boss) _team -= 0.12f;   // the dragon hits back hard
            if (_enemy <= 0f) End(true);
            else if (_team <= 0f) End(false);
        }
    }

    void Cast(int skill)
    {
        if (_over) return;
        Cooldown(skills[skill]);
        Hit(SkillDamage);
        if (_enemy <= 0f) End(true);
    }

    async void Cooldown(Button skill)
    {
        skill.interactable = false;
        try { await skill.GetComponent<UIAnimation>().PlayAsync("Cooldown"); }
        catch (OperationCanceledException) { return; }   // the battle page closed
        if (this != null && !_over) skill.interactable = true;
    }

    void Hit(int damage)
    {
        _enemy = Mathf.Max(0f, _enemy - damage);
        enemy.Play("Hit");
        var number = numbers[_next++ % numbers.Length];
        number.text = damage.ToString("N0");
        ((RectTransform)number.transform.parent).anchoredPosition = new Vector2(UnityEngine.Random.Range(-160f, 160f), UnityEngine.Random.Range(-40f, 80f));
        number.GetComponent<UIAnimation>().Play("Pop");
    }

    async void End(bool won)
    {
        if (_over) return;
        _over = true;
        foreach (var s in skills) s.interactable = false;
        giveUp.interactable = false;
        try
        {
            if (won) await enemy.PlayAsync("Die");
            else await GetComponent<UIAnimation>().PlayAsync("Lose");
        }
        catch (OperationCanceledException) { return; }
        RpgDemo.Instance.EndBattle(won);
    }

    void Update() => Bars(false);

    // Health bars ease toward their values (the fill's width, so its round ends stay round).
    void Bars(bool snap)
    {
        var k = snap ? 1f : 1f - Mathf.Exp(-10f * Time.deltaTime);
        Ease(enemyHp, _enemy / Mathf.Max(1f, _enemyMax), k);
        foreach (var hp in teamHp) Ease(hp, Mathf.Max(0f, _team), k);
    }

    static void Ease(RectTransform fill, float to, float k)
    {
        var max = fill.anchorMax;
        max.x = Mathf.Lerp(max.x, to, k);
        fill.anchorMax = max;
    }
}
