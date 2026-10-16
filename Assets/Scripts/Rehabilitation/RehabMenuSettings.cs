using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the Difficulty and Hand buttons on the Rehabilitation Menu.
/// Buttons in each array are ordered by enum value
/// (difficulty: Easy, Medium, Hard / hand: Right, Left, Both).
/// </summary>
public class RehabMenuSettings : MonoBehaviour
{
    public Button[] difficultyButtons;
    public Button[] handButtons;

    public Color selectedColor = new Color(0.16f, 0.68f, 0.45f, 1f);
    public Color unselectedColor = new Color(0.30f, 0.36f, 0.46f, 1f);

    private void Start()
    {
        Refresh();
    }

    public void SetDifficulty(int level)
    {
        RehabSettings.Difficulty = (RehabDifficulty)level;
        Refresh();
    }

    public void SetHand(int hand)
    {
        RehabSettings.Hand = (RehabHand)hand;
        Refresh();
    }

    private void Refresh()
    {
        Paint(difficultyButtons, (int)RehabSettings.Difficulty);
        Paint(handButtons, (int)RehabSettings.Hand);
    }

    private void Paint(Button[] buttons, int selected)
    {
        if (buttons == null) return;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].image != null)
                buttons[i].image.color = i == selected ? selectedColor : unselectedColor;
        }
    }
}
