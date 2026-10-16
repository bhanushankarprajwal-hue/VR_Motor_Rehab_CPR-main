using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

/// <summary>
/// Object Placement exercise. The patient picks up the block and places it
/// on the green zone. After each correct placement the block returns to its
/// start spot and the zone moves to a new side. Difficulty and hand come
/// from the Rehabilitation Menu.
/// </summary>
public class TargetZone : MonoBehaviour
{
    public GameObject exercisePanel;
    public GameObject completionPanel;

    [Header("Settings Source")]
    [Tooltip("When on, difficulty and hand come from the Rehabilitation Menu")]
    public bool useMenuSettings = true;

    [Header("Exercise Settings")]
    public int targetRepetitions = 10;
    public float requiredMovementDistance = 0.5f;
    public float placementDistance = 0.35f;
    [Tooltip("Move the zone to a new side each repetition, based on difficulty")]
    public bool autoPlaceTarget = true;
    [Tooltip("Seconds before the block returns to its start spot after a correct placement")]
    public float resetDelay = 1.0f;
    [Tooltip("If the block falls below this height (m) it is put back automatically")]
    public float fallResetHeight = 0.3f;
    [Tooltip("Extra distance (m) allowed so a block overlapping the zone edge still counts")]
    public float placementGrace = 0.05f;

    [Header("UI")]
    public TMP_Text repetitionText;
    public TMP_Text exerciseText;
    public TMP_Text completionResultText;

    [Header("Colors")]
    public Color normalColor = new Color(0.2f, 0.8f, 0.4f, 1f);
    public Color correctColor = new Color(1f, 0.85f, 0.2f, 1f);

    private enum HandChoice { Right, Left, Both }
    private HandChoice hand = HandChoice.Both;

    private int repetitions = 0;
    private int attempts = 0;
    private bool exerciseCompleted = false;
    private bool exerciseStarted = false;
    private bool resetting = false;
    private bool grabbedWithAllowedHand = true;

    private Renderer targetRenderer;
    private XRGrabInteractable grabInteractable;
    private Rigidbody objectBody;
    private Vector3 objectStartPosition;
    private Quaternion objectStartRotation;
    private Vector3 grabStartPosition;
    private float maximumDistanceMoved;
    private float zoneHeight;
    private int sideIndex = 0;

    private float sessionStartTime = -1f;
    private float repStartTime = -1f;
    private readonly List<float> repTimes = new List<float>();
    private Coroutine checkRoutine;

    private void Start()
    {
        if (useMenuSettings)
        {
            targetRepetitions = RehabSettings.PlacementReps;
            requiredMovementDistance = RehabSettings.PlacementMoveDistance;
            placementDistance = RehabSettings.PlacementTolerance;
            hand = (HandChoice)(int)RehabSettings.Hand;
        }

        targetRenderer = GetComponent<Renderer>();
        SetZoneColor(normalColor);
        zoneHeight = transform.position.y;

        GameObject rehabObject = GameObject.FindGameObjectWithTag("RehabObject");
        if (rehabObject != null)
        {
            grabInteractable = rehabObject.GetComponent<XRGrabInteractable>();
            objectBody = rehabObject.GetComponent<Rigidbody>();
            objectStartPosition = rehabObject.transform.position;
            objectStartRotation = rehabObject.transform.rotation;

            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.AddListener(OnGrab);
                grabInteractable.selectExited.AddListener(OnRelease);
            }
        }

        if (autoPlaceTarget)
        {
            Vector3 s = transform.localScale;
            transform.localScale = new Vector3(placementDistance * 2f, s.y, placementDistance * 2f);
            PlaceTargetForNextRep();
        }

        if (exercisePanel != null) exercisePanel.SetActive(true);
        if (completionPanel != null) completionPanel.SetActive(false);

        UpdateRepetitionUI();
        ShowInstruction("");
    }

    private void Update()
    {
        if (grabInteractable == null) return;

        // Put the block back if it falls off the table
        if (!resetting && !grabInteractable.isSelected &&
            grabInteractable.transform.position.y < fallResetHeight)
        {
            ResetObject();
            ShowInstruction("The block fell - it is back on the table.");
        }

        if (!exerciseStarted) return;

        float distance = Vector3.Distance(grabStartPosition, grabInteractable.transform.position);
        if (distance > maximumDistanceMoved)
            maximumDistanceMoved = distance;
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        if (exerciseCompleted || resetting) return;

        grabbedWithAllowedHand = IsAllowedHand(args.interactorObject.transform);
        if (!grabbedWithAllowedHand)
            ShowInstruction("Please use your " + HandLabel() + ".");

        if (checkRoutine != null) { StopCoroutine(checkRoutine); checkRoutine = null; }
        exerciseStarted = true;
        grabStartPosition = grabInteractable.transform.position;
        maximumDistanceMoved = 0f;
        SetZoneColor(normalColor);

        if (sessionStartTime < 0f) sessionStartTime = Time.time;
        if (repStartTime < 0f) repStartTime = Time.time;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (exerciseCompleted || resetting || !exerciseStarted) return;
        exerciseStarted = false;

        if (!grabbedWithAllowedHand)
        {
            ShowInstruction("That one did not count - use your " + HandLabel() + ".");
            return;
        }

        // Wait for the block to settle, then check where it landed
        if (checkRoutine != null) StopCoroutine(checkRoutine);
        checkRoutine = StartCoroutine(CheckPlacementWhenSettled());
    }

    private IEnumerator CheckPlacementWhenSettled()
    {
        float waited = 0f;
        while (waited < 2f)
        {
            yield return new WaitForFixedUpdate();
            waited += Time.fixedDeltaTime;

            if (grabInteractable.isSelected) { checkRoutine = null; yield break; } // picked up again
            bool still = objectBody == null || objectBody.isKinematic || objectBody.linearVelocity.magnitude < 0.05f;
            if (waited > 0.3f && still) break;
        }

        checkRoutine = null;
        attempts++;

        Vector3 a = grabInteractable.transform.position;
        Vector3 b = transform.position;
        a.y = 0f;
        b.y = 0f;
        float distanceToTarget = Vector3.Distance(a, b);
        Vector3 start = objectStartPosition; start.y = 0f;
        float distanceFromStart = Vector3.Distance(a, start);

        // Counts if the block overlaps the zone (zone radius + a little for the block's size)
        bool onTarget = distanceToTarget <= placementDistance + placementGrace;
        bool movedEnough = Mathf.Max(maximumDistanceMoved, distanceFromStart) >= requiredMovementDistance;

        Debug.Log(string.Format("[ObjectPlacement] Released. Distance to zone: {0:F2} m (limit {1:F2}), moved: {2:F2} m (need {3:F2})",
            distanceToTarget, placementDistance + placementGrace, Mathf.Max(maximumDistanceMoved, distanceFromStart), requiredMovementDistance));

        if (movedEnough && onTarget)
        {
            repetitions = Mathf.Min(repetitions + 1, targetRepetitions);
            repTimes.Add(Time.time - repStartTime);

            PlayerPrefs.SetInt("ObjectPlacementRepetitions", repetitions);
            PlayerPrefs.Save();

            SetZoneColor(correctColor);
            UpdateRepetitionUI();

            if (repetitions >= targetRepetitions)
            {
                FinishExercise();
                yield break;
            }

            ShowInstruction("Well done! Get ready for the next one.");
            StartCoroutine(ResetForNextRep());
        }
        else
        {
            SetZoneColor(normalColor);
            if (!onTarget)
                ShowInstruction(string.Format("Almost! The block is {0:F0} cm from the zone.", Mathf.Max(0f, distanceToTarget - placementDistance) * 100f));
            else
                ShowInstruction("Pick up the block and move it further.");
        }
    }

    private IEnumerator ResetForNextRep()
    {
        resetting = true;
        yield return new WaitForSeconds(resetDelay);

        ResetObject();
        if (autoPlaceTarget) PlaceTargetForNextRep();
        SetZoneColor(normalColor);
        repStartTime = Time.time;
        resetting = false;
        ShowInstruction("");
    }

    private void ResetObject()
    {
        if (grabInteractable == null) return;

        if (objectBody != null)
        {
            objectBody.linearVelocity = Vector3.zero;
            objectBody.angularVelocity = Vector3.zero;
            objectBody.position = objectStartPosition;
            objectBody.rotation = objectStartRotation;
        }
        grabInteractable.transform.SetPositionAndRotation(objectStartPosition, objectStartRotation);
    }

    private void PlaceTargetForNextRep()
    {
        float offset = RehabSettings.PlacementOffset;
        float direction;

        if (hand == HandChoice.Right) direction = 1f;
        else if (hand == HandChoice.Left) direction = -1f;
        else direction = sideIndex % 2 == 0 ? 1f : -1f;

        sideIndex++;

        Vector3 p = objectStartPosition + Vector3.right * (direction * offset);
        p.y = zoneHeight;
        transform.position = p;
    }

    private void FinishExercise()
    {
        exerciseCompleted = true;
        repetitions = targetRepetitions;
        UpdateRepetitionUI();

        float total = sessionStartTime >= 0f ? Time.time - sessionStartTime : 0f;
        float avg = Average(repTimes);
        float best = Min(repTimes);
        float accuracy = attempts > 0 ? 100f * repetitions / attempts : 0f;

        PlayerPrefs.SetInt("ObjectPlacementRepetitions", targetRepetitions);
        PlayerPrefs.SetInt("ObjectPlacementCompleted", 1);
        PlayerPrefs.Save();

        RehabSessionHistory.Add(new RehabSessionRecord
        {
            exercise = "Object Placement",
            difficulty = RehabSettings.DifficultyName,
            hand = hand.ToString(),
            reps = repetitions,
            attempts = attempts,
            totalSeconds = total,
            avgSecondsPerRep = avg,
            bestRepSeconds = best,
            maxReachMetres = 0f,
            avgReachMetres = 0f,
            accuracyPercent = accuracy
        });

        if (completionResultText != null)
        {
            completionResultText.text = string.Format(
                "Reps: {0}   |   Time: {1:F0}s\nAvg per rep: {2:F1}s   |   Fastest: {3:F1}s\nAccuracy: {4:F0}%",
                repetitions, total, avg, best, accuracy);
        }

        if (exercisePanel != null) exercisePanel.SetActive(false);
        if (completionPanel != null) completionPanel.SetActive(true);
    }

    private bool IsAllowedHand(Transform interactor)
    {
        if (hand == HandChoice.Both || interactor == null) return true;

        bool isLeft = false, isRight = false;
        for (Transform t = interactor; t != null; t = t.parent)
        {
            if (t.name.Contains("Left")) { isLeft = true; break; }
            if (t.name.Contains("Right")) { isRight = true; break; }
        }

        if (!isLeft && !isRight) return true;
        return hand == HandChoice.Right ? isRight : isLeft;
    }

    private string HandLabel()
    {
        return hand == HandChoice.Right ? "right hand" : hand == HandChoice.Left ? "left hand" : "either hand";
    }

    private void ShowInstruction(string feedback)
    {
        if (exerciseText == null) return;

        string text = "Exercise: OBJECT PLACEMENT\nMove the block onto the green zone with your " +
                      HandLabel() + ".\nLevel: " + RehabSettings.DifficultyName;

        if (!string.IsNullOrEmpty(feedback))
            text += "\n<b>" + feedback + "</b>";

        exerciseText.text = text;
    }

    private void SetZoneColor(Color c)
    {
        if (targetRenderer != null) targetRenderer.material.color = c;
    }

    private void UpdateRepetitionUI()
    {
        if (repetitionText != null)
            repetitionText.text = "REPETITIONS\n" + repetitions + " / " + targetRepetitions;
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrab);
            grabInteractable.selectExited.RemoveListener(OnRelease);
        }
    }

    private static float Average(List<float> values)
    {
        if (values.Count == 0) return 0f;
        float sum = 0f;
        foreach (float v in values) sum += v;
        return sum / values.Count;
    }

    private static float Min(List<float> values)
    {
        if (values.Count == 0) return 0f;
        float m = values[0];
        foreach (float v in values) if (v < m) m = v;
        return m;
    }
}
