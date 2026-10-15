using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Unit information: the three ranks' thumbnails are tabs over a sheet container of portraits; Expand opens the portrait viewer.</summary>
public sealed class UsnUnitInfoModal : MonoBehaviour, IUIAnimScreenLifecycle
{
    public TMP_Text title, description;
    public Button close, expand;
    [Tooltip("Rank 1..3 thumbnails, also the tabs.")]
    public Image[] thumbnails;
    public UIAnimSheetContainer sheets;
    public UIAnimScreen portraitSheet;
    [Tooltip("Per unit (003, 004, 005) × rank 1..3.")]
    public Sprite[] thumbnailSprites, portraitSprites;
    static readonly string[] Names = { "Unknown Dragon", "Unknown Horse", "Unknown Whale" };   // master data UnitType 003..005

    int _unit;

    /// <summary>The portrait area: where the viewer's portrait comes from and goes back to.</summary>
    public RectTransform PortraitArea => (RectTransform)sheets.transform;

    void Awake()
    {
        close.onClick.AddListener(() => UsnDemo.Instance.Back());
        expand.onClick.AddListener(() => UsnDemo.Instance.ShowPortrait(portraitSprites[_unit * 3 + sheets.ActiveIndex]));
        for (var i = 0; i < thumbnails.Length; i++)
        {
            var rank = i;
            thumbnails[i].GetComponent<Button>().onClick.AddListener(() => Select(rank));
        }
    }

    // The unit is the one whose shop thumbnail was tapped.
    public async Awaitable WillEnter(UIAnimTransition t)
    {
        var unit = _unit = UsnDemo.Instance.SelectedUnit;
        title.text = Names[unit];
        for (var rank = 0; rank < 3; rank++)
        {
            thumbnails[rank].sprite = thumbnailSprites[unit * 3 + rank];
            var index = sheets.Register(portraitSheet);
            sheets.Sheets[index].GetComponentInChildren<Image>().sprite = portraitSprites[unit * 3 + rank];
        }
        await Show(0, false);
    }

    async void Select(int rank)
    {
        if (UIAnimScreenContainer.AnyInTransition || rank == sheets.ActiveIndex) return;
        await Show(rank, true);
    }

    Awaitable Show(int rank, bool animated)
    {
        description.text = $"This is the description text for the unit {Names[_unit]} with a rank of {rank + 1}.";
        return sheets.Show(rank, animated);
    }
}
