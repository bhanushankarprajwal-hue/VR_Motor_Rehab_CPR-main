using System.Collections;
using UnityEngine;

/// <summary>
/// Places a world-space UI panel within arm's reach in front of the player,
/// so buttons can be pressed with a finger (hand tracking poke).
/// Works seated or standing because it uses the real head position.
/// </summary>
public class PanelPlacer : MonoBehaviour
{
    [Tooltip("Player head / main camera. Found automatically if empty.")]
    public Transform head;
    [Tooltip("Distance in front of the head (metres). 0.4 - 0.5 is easy to touch.")]
    public float distance = 0.45f;
    [Tooltip("Height relative to the eyes (metres). Negative = below eye level.")]
    public float heightOffset = -0.15f;
    [Tooltip("Real-world width of the panel (metres). 0 = keep current scale.")]
    public float panelWidthMetres = 0.6f;

    [Header("When to place")]
    public bool placeOnStart = true;
    [Tooltip("Wait for head tracking to settle before placing")]
    public float startDelay = 0.5f;
    [Tooltip("Optional: move the panel in front of the player whenever this object becomes active (e.g. a completion panel)")]
    public GameObject placeWhenActive;

    private bool placedForWatch;

    private IEnumerator Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;

        if (placeOnStart)
        {
            yield return new WaitForSeconds(startDelay);
            Place();
        }
    }

    private void Update()
    {
        if (placeWhenActive == null) return;

        bool isActive = placeWhenActive.activeInHierarchy;
        if (isActive && !placedForWatch)
        {
            Place();
            placedForWatch = true;
        }
        else if (!isActive)
        {
            placedForWatch = false;
        }
    }

    public void Place()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (head == null) return;

        var rt = transform as RectTransform;
        if (rt != null && panelWidthMetres > 0f && rt.rect.width > 0f)
            transform.localScale = Vector3.one * (panelWidthMetres / rt.rect.width);

        Vector3 forward = head.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        transform.position = head.position + forward * distance + Vector3.up * heightOffset;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
}
