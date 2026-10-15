using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Story scene: the speaker slides in from their side ("EnterLeft" / "EnterRight"), the name plate pops and the line
/// types itself out ("Type", Text Reveal). A tap finishes the line at once (Complete), the next tap goes on. After the
/// last line two choices drop in ("Choices"); picking one closes the scene and says so (a toast).
/// </summary>
public sealed class RpgStory : MonoBehaviour, IUIAnimScreenLifecycle
{
    public Image left, right;
    public TMP_Text speaker, line;
    public Button next;
    public Button[] choices;

    int _line = -1;
    UIAnimation _animation;

    public int Line => _line;
    public bool Typing => _animation.IsPlaying("Type");

    void Awake()
    {
        _animation = GetComponent<UIAnimation>();
        next.onClick.AddListener(Next);
        for (var i = 0; i < choices.Length; i++)
        {
            var choice = RpgData.StoryChoices[i];
            choices[i].GetComponentInChildren<TMP_Text>().text = choice;
            choices[i].onClick.AddListener(() => Choose(choice));
        }
    }

    public Awaitable WillEnter(UIAnimTransition t)
    {
        _line = -1;
        _animation.Sample("Choices", 0f);
        line.text = "";
        speaker.text = "";
        left.color = right.color = new Color(1, 1, 1, 0);
        return UIAnimScreen.Completed();
    }

    public void DidEnter(UIAnimTransition t) => Next();

    void Next()
    {
        if (Typing)
        {
            _animation.Complete("Type");   // the whole line at once
            return;
        }
        if (_line + 1 >= RpgData.Story.Length) return;
        _line++;
        var (who, hero, isRight, text) = RpgData.Story[_line];
        var demo = RpgDemo.Instance;
        var portrait = isRight ? right : left;
        var sameSpeaker = _line > 0 && RpgData.Story[_line - 1].who == who;
        portrait.sprite = demo.Sprite(hero);
        speaker.text = who;
        line.text = text;
        if (!sameSpeaker) _animation.Play(isRight ? "EnterRight" : "EnterLeft");
        _animation.Play("Type");
        if (_line == RpgData.Story.Length - 1) ShowChoices();
    }

    async void ShowChoices()
    {
        while (this != null && Typing) await Awaitable.NextFrameAsync();
        if (this != null) _animation.Play("Choices");
    }

    void Choose(string choice)
    {
        var demo = RpgDemo.Instance;
        demo.Back();
        demo.Toast($"You chose: {choice}");
    }
}
