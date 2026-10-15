using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A hero in the heroes grid. Tapping it opens the hero's page, whose portrait grows out of this one.</summary>
public sealed class RpgHeroCard : MonoBehaviour
{
    public Image portrait, frame;
    public TMP_Text heroName, level;
    public GameObject[] stars;

    int _index;

    void Awake() => GetComponentInChildren<Button>().onClick.AddListener(() => RpgDemo.Instance.ShowHero(_index, portrait.rectTransform));

    public void Set(int index)
    {
        _index = index;
        var hero = RpgData.Heroes[index];
        portrait.sprite = RpgDemo.Instance.Sprite(hero.id);
        frame.color = RpgData.StarColor(hero.stars);
        heroName.text = hero.name;
        level.text = "Lv. " + RpgDemo.Instance.Level(index);
        for (var i = 0; i < stars.Length; i++) stars[i].SetActive(i < hero.stars);
    }
}
