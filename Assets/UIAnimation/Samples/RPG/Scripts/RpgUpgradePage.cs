using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Upgrade and equip one hero (opened from the hero's page). UPGRADE spends gold: the page plays "LevelUp" (a flash,
/// the level punches, the stats bump) or the button shakes ("Deny") when gold is short. AUTO EQUIP drops the hero's
/// gear into the empty slots one after another (each slot's "Equip"); the power counts up with every change.
/// </summary>
public sealed class RpgUpgradePage : MonoBehaviour, IUIAnimScreenLifecycle
{
    public Image portrait;
    public TMP_Text heroName, level, cost, power;
    public TMP_Text[] stats;
    public Button upgrade, autoEquip;
    public Image[] slotIcons;
    [Min(0)] public float equipStagger = 0.25f;

    const int GearPower = 350;

    int _hero, _shownPower, _targetPower;
    bool[] _equipped = new bool[3];
    float _count = 1f;
    int _countFrom;
    UIAnimation _animation;

    void Awake()
    {
        _animation = GetComponent<UIAnimation>();
        upgrade.onClick.AddListener(Upgrade);
        autoEquip.onClick.AddListener(AutoEquip);
    }

    public Awaitable WillEnter(UIAnimTransition t)
    {
        if (t.Kind != UIAnimTransitionKind.Push) return UIAnimScreen.Completed();
        var demo = RpgDemo.Instance;
        _hero = demo.SelectedHero;
        var hero = RpgData.Heroes[_hero];
        portrait.sprite = demo.Sprite(hero.id);
        heroName.text = hero.name;
        _equipped = new bool[3];
        for (var i = 0; i < slotIcons.Length; i++)
        {
            slotIcons[i].sprite = demo.Sprite(GearIcon(hero, i));
            slotIcons[i].GetComponentInParent<UIAnimation>().Sample("Equip", 0f);   // empty
        }
        Refresh();
        _shownPower = _targetPower;
        _count = 1f;
        power.text = _shownPower.ToString("N0");
        return UIAnimScreen.Completed();
    }

    static string GearIcon(RpgData.Hero hero, int slot) => slot switch { 0 => hero.weapon, 1 => "armor", _ => "ring" };

    void Refresh()
    {
        var demo = RpgDemo.Instance;
        var hero = RpgData.Heroes[_hero];
        var lv = demo.Level(_hero);
        level.text = "Lv. " + lv;
        cost.text = RpgDemo.UpgradeCost(lv).ToString("N0");
        var scale = lv / (float)hero.level;
        for (var i = 0; i < stats.Length; i++) stats[i].text = Mathf.RoundToInt(RpgData.Stat(hero, i) * scale).ToString("N0");
        var p = Mathf.RoundToInt((hero.atk + hero.def + hero.hp / 10f + hero.spd) * scale);
        foreach (var e in _equipped) if (e) p += GearPower;
        CountTo(p);
    }

    void CountTo(int value)
    {
        _countFrom = _shownPower;
        _targetPower = value;
        _count = 0f;
    }

    void Update()
    {
        if (_count >= 1f) return;
        _count = Mathf.Min(1f, _count + Time.deltaTime / 0.6f);
        _shownPower = Mathf.RoundToInt(Mathf.Lerp(_countFrom, _targetPower, 1f - (1f - _count) * (1f - _count)));
        power.text = _shownPower.ToString("N0");
    }

    void Upgrade()
    {
        if (!RpgDemo.Instance.Upgrade(_hero))
        {
            upgrade.GetComponent<UIAnimation>().Play("Deny");
            return;
        }
        Refresh();
        _animation.Play("LevelUp");
    }

    async void AutoEquip()
    {
        autoEquip.interactable = false;
        for (var i = 0; i < slotIcons.Length; i++)
        {
            if (_equipped[i]) continue;
            _equipped[i] = true;
            slotIcons[i].GetComponentInParent<UIAnimation>().Play("Equip");
            Refresh();
            await Awaitable.WaitForSecondsAsync(equipStagger);
        }
        autoEquip.interactable = true;
    }

    public bool Equipped(int slot) => _equipped[slot];
}
