using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adventure: a chapter carousel (scroll list, the centered card grows: scrub state "Focus") and the chosen chapter's
/// ten stages, which pop in along the path ("Stages" on the page) every time the chapter changes. START plays the stage.
/// </summary>
public sealed class RpgAdventurePage : MonoBehaviour
{
    public UIAnimScrollList chapters;
    public RpgStageNode[] stages;
    public TMP_Text chapterTitle, stageInfo;
    public Button start;

    int _chapter = -1, _stage;
    UIAnimation _animation;

    void Awake()
    {
        _animation = GetComponent<UIAnimation>();
        chapters.SetData(RpgData.Chapters, (RpgRow card, RpgData.Chapter c, int _) =>
        {
            card.icon.sprite = RpgDemo.Instance.Sprite(c.icon);
            card.title.text = c.name;
            card.text.text = $"{c.cleared}/10";
        });
        chapters.CurrentIndexChanged += ShowChapter;
        for (var i = 0; i < stages.Length; i++)
        {
            var stage = i;
            stages[i].GetComponent<Button>().onClick.AddListener(() => Select(stage));
        }
        start.onClick.AddListener(() => RpgDemo.Instance.StartStage(_chapter, _stage));
    }

    void OnEnable()
    {
        if (_chapter < 0) ShowChapter(chapters.CurrentIndex);
    }

    void ShowChapter(int chapter)
    {
        if (chapter == _chapter) return;
        _chapter = chapter;
        var cleared = RpgData.Chapters[chapter].cleared;
        var open = chapter == 0 || RpgData.Chapters[chapter - 1].cleared >= 10;
        chapterTitle.text = $"CHAPTER {chapter + 1}  ·  {RpgData.Chapters[chapter].name.ToUpperInvariant()}";
        for (var i = 0; i < stages.Length; i++)
            stages[i].Set(i, !open ? RpgStageNode.State.Locked : i < cleared ? RpgStageNode.State.Cleared : i == cleared ? RpgStageNode.State.Next : RpgStageNode.State.Locked);
        Select(open ? Mathf.Min(cleared, stages.Length - 1) : -1);
        _animation.Play("Stages");
    }

    void Select(int stage)
    {
        _stage = stage;
        for (var i = 0; i < stages.Length; i++) stages[i].Select(i == stage);
        start.interactable = stage >= 0;
        stageInfo.text = stage < 0 ? "Clear the previous chapter first" : $"STAGE {_chapter + 1}-{stage + 1}{(stage == 9 ? "  ·  BOSS" : "")}  ·  {RpgDemo.StageStamina} stamina";
    }
}
