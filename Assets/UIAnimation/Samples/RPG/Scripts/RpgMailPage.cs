using System;
using System.Collections.Generic;
using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mailbox: a list of letters. CLAIM on one plays its "Claim" (the reward lifts off, the letter fades), pays and folds the
/// row away (ResizeItem, then RemoveItem). CLAIM ALL does that to every letter, one after another. When the box is
/// empty, the empty note fades in ("Empty" on the page).
/// </summary>
public sealed class RpgMailPage : MonoBehaviour
{
    public UIAnimScrollList list;
    public Button claimAll;
    public TMP_Text count;
    [Min(0)] public float foldSeconds = 0.25f;

    List<RpgData.Mail> _mails;
    bool _claiming;

    public int MailCount => _mails.Count;

    void Awake()
    {
        _mails = RpgDemo.Instance.Mails;
        list.SetCount(_mails.Count, Bind);
        claimAll.onClick.AddListener(ClaimAll);
        Refresh();
        if (_mails.Count == 0) GetComponent<UIAnimation>().Sample("Empty", 1f);
    }

    void Bind(RectTransform item, int i)
    {
        var row = item.GetComponent<RpgMailRow>();
        var mail = _mails[i];
        row.Set(mail);
        item.GetComponent<UIAnimation>().Sample("Claim", 0f);
        row.claim.onClick.RemoveAllListeners();
        row.claim.onClick.AddListener(() => ClaimOne(mail));
    }

    void Refresh()
    {
        count.text = $"{_mails.Count} LETTERS";
        claimAll.interactable = _mails.Count > 0 && !_claiming;
    }

    async void ClaimOne(RpgData.Mail mail)
    {
        if (_claiming) return;
        _claiming = true;
        Refresh();
        await Claim(mail);
        _claiming = false;
        Refresh();
    }

    async void ClaimAll()
    {
        if (_claiming) return;
        _claiming = true;
        Refresh();
        while (_mails.Count > 0) await Claim(_mails[0]);
        _claiming = false;
        Refresh();
    }

    async Awaitable Claim(RpgData.Mail mail)
    {
        var index = _mails.IndexOf(mail);
        var item = list.GetItem(index);
        if (item != null)
        {
            try { await item.GetComponent<UIAnimation>().PlayAsync("Claim"); }
            catch (OperationCanceledException) { }   // the row was reused or the page closed: pay anyway
        }
        RpgDemo.Instance.Add(gold: mail.gold, gems: mail.gems, stamina: mail.stamina);
        await list.ResizeItem(index, 0f, foldSeconds);
        _mails.RemoveAt(index);
        list.RemoveItem(index);
        Refresh();
        if (_mails.Count == 0) GetComponent<UIAnimation>().Play("Empty");
    }
}
