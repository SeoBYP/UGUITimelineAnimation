using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A summoned card: drops in face down ("Drop"), then turns over ("Flip", or "FlipEpic" / "FlipLegend" for 4★ / 5★).</summary>
public sealed class RpgSummonCard : MonoBehaviour
{
    public Image portrait, frame;
    public TMP_Text heroName;
    public GameObject[] stars;

    public int Stars { get; private set; }
    public UIAnimation Animation => GetComponent<UIAnimation>();
    public string FlipState => Stars >= 5 ? "FlipLegend" : Stars == 4 ? "FlipEpic" : "Flip";

    public void Set(RpgData.Hero hero)
    {
        Stars = hero.stars;
        portrait.sprite = RpgDemo.Instance.Sprite(hero.id);
        frame.color = RpgData.StarColor(hero.stars);
        heroName.text = hero.name;
        for (var i = 0; i < stars.Length; i++) stars[i].SetActive(i < hero.stars);
    }
}
