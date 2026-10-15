using System;
using System.Linq;
using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Formation before a stage (opened by START on the adventure page): tap a hero to put them in the next free slot
/// (the slot's "In": the portrait drops in), tap a slot to take them out (the same state reversed). The team power counts.
/// BATTLE plays the page's "Battle" (the team charges off, the screen flashes) and opens the battle.
/// </summary>
public sealed class RpgFormation : MonoBehaviour, IUIAnimScreenLifecycle
{
    public Image[] slotPortraits;
    public Button[] slots, roster;
    public TMP_Text power, stage;
    public Button battle;

    readonly int[] _team = { -1, -1, -1, -1, -1 };
    int _shown, _target, _from;
    float _count = 1f;
    bool _charging;

    public int TeamSize => _team.Count(h => h >= 0);

    void Awake()
    {
        for (var i = 0; i < roster.Length; i++)
        {
            var hero = i;
            roster[i].onClick.AddListener(() => Add(hero));
        }
        for (var i = 0; i < slots.Length; i++)
        {
            var slot = i;
            slots[i].onClick.AddListener(() => Remove(slot));
        }
        battle.onClick.AddListener(Battle);
    }

    public Awaitable WillEnter(UIAnimTransition t)
    {
        if (t.Kind != UIAnimTransitionKind.Push) return UIAnimScreen.Completed();
        var demo = RpgDemo.Instance;
        var (chapter, s) = demo.SelectedStage;
        stage.text = $"STAGE {chapter + 1}-{s + 1}";
        for (var i = 0; i < roster.Length; i++) roster[i].transform.Find("Portrait").GetComponent<Image>().sprite = demo.Sprite(RpgData.Heroes[i].id);
        // The strongest three start in the team.
        for (var i = 0; i < _team.Length; i++) _team[i] = i < 3 ? i : -1;
        for (var i = 0; i < slots.Length; i++)
        {
            if (_team[i] >= 0) slotPortraits[i].sprite = demo.Sprite(RpgData.Heroes[_team[i]].id);
            slots[i].GetComponent<UIAnimation>().Sample("In", _team[i] >= 0 ? 1f : 0f);
        }
        RefreshRoster();
        _shown = _target = Power();
        power.text = _shown.ToString("N0");
        return UIAnimScreen.Completed();
    }

    int Power()
    {
        var demo = RpgDemo.Instance;
        return _team.Where(h => h >= 0).Sum(h =>
        {
            var hero = RpgData.Heroes[h];
            return Mathf.RoundToInt((hero.atk + hero.def + hero.hp / 10f + hero.spd) * demo.Level(h) / hero.level);
        });
    }

    void RefreshRoster()
    {
        for (var i = 0; i < roster.Length; i++)
        {
            var inTeam = _team.Contains(i);
            roster[i].interactable = !inTeam;
            roster[i].GetComponent<CanvasGroup>().alpha = inTeam ? 0.35f : 1f;
        }
        battle.interactable = TeamSize > 0;
        _from = _shown;
        _target = Power();
        _count = 0f;
    }

    void Add(int hero)
    {
        var slot = System.Array.IndexOf(_team, -1);
        if (slot < 0 || _team.Contains(hero) || _charging) return;
        _team[slot] = hero;
        slotPortraits[slot].sprite = RpgDemo.Instance.Sprite(RpgData.Heroes[hero].id);
        slots[slot].GetComponent<UIAnimation>().Play("In");
        RefreshRoster();
    }

    void Remove(int slot)
    {
        if (_team[slot] < 0 || _charging) return;
        _team[slot] = -1;
        slots[slot].GetComponent<UIAnimation>().PlayReversed("In");
        RefreshRoster();
    }

    void Update()
    {
        if (_count >= 1f) return;
        _count = Mathf.Min(1f, _count + Time.deltaTime / 0.5f);
        _shown = Mathf.RoundToInt(Mathf.Lerp(_from, _target, 1f - (1f - _count) * (1f - _count)));
        power.text = _shown.ToString("N0");
    }

    async void Battle()
    {
        if (_charging || UIAnimScreenContainer.AnyInTransition) return;
        var demo = RpgDemo.Instance;
        if (demo.Stamina < RpgDemo.StageStamina)
        {
            battle.GetComponent<UIAnimation>().Play("Deny");
            return;
        }
        _charging = true;
        var anim = GetComponent<UIAnimation>();
        try { await anim.PlayAsync("Battle"); }
        catch (OperationCanceledException) { _charging = false; return; }
        var (chapter, s) = demo.SelectedStage;
        demo.Team = _team.Where(h => h >= 0).ToArray();
        demo.BeginBattle(chapter, s);
        anim.Sample("Battle", 0f);   // the team is back in place under the result popup
        _charging = false;
    }
}
