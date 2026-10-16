using UnityEngine;
using UnityEngine.EventSystems;

public class SelectContinueButton : MonoBehaviour
{
    public GameObject continueButton;

    private void OnEnable()
    {
        EventSystem.current.SetSelectedGameObject(continueButton);
    }
}