using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Target Reach exercise. A glowing ball appears within arm's reach and the
/// patient touches it. Targets are placed around the player's real head
/// position, so it works seated or standing and for any height.
/// Difficulty and hand come from the Rehabilitation Menu.
/// </summary>
public class TargetReach : MonoBehaviour
{
    public enum HandMode { Right, Left, Both }

    [Header("Controllers / Hands")]
    [Tooltip("Right hand / controller")]
    public Transform controllerTransform;
    [Tooltip("Left hand / controller")]
    public Transform leftControllerTransform;
    [Tooltip("Right hand tracking point (index fingertip). Used when hand tracking is active.")]
    public Transform rightHandPoint;
    [Tooltip("Left hand tracking point (index fingertip). Used when hand tracking is active.")]
    public Transform leftHandPoint;
    [Tooltip("Which hand(s) can reach the target")]
    public HandMode handMode = HandMode.Both;

    [Header("Settings Source")]
    [Tooltip("When on, difficulty and hand come from the Rehabilitation Menu")]
    public bool useMenuSettings = true;

    [Header("Automatic Target Placement")]
    [Tooltip("Place targets around the player's real head position (works seated or standing)")]
    public bool placeTargetsAroundPlayer = true;
    public Transform headTransform;
    [Tooltip("How far below eye level the centre of the target area is (metres)")]
    public float heightBelowEyes = 0.25f;
    [Tooltip("Seconds of countdown before the exercise starts")]
    public int countdownSeconds = 3;

    [Header("Exercise Settings")]
    public int targetRepetitions = 10;
    public float reachDistance = 0.25f;

    [Header("UI")]
    public TMP_Text repetitionText;
    public Slider progressBar;
    public TMP_Text statusText;
    public TMP_Text exerciseText;

    [Header("Completion UI")]
    public GameObject exercisePanel;
    public GameObject completionPanel;
    public TMP_Text completionResultText;

    [Header("Target Positions (used when automatic placement is off)")]
    public Transform[] targetPositions;

    [Header("Target Animation")]
    public float pulseSpeed = 2f;
    public float pulseAmount = 0.08f;
    public float moveSpeed = 3f;

    private int repetitions = 0;
    private int currentTarget = 0;
    private bool exerciseCompleted = false;
    private bool exerciseRunning = false;
    private bool movingToNextTarget = false;

    private Vector3 originalScale;
    private Vector3 targetPosition;
    private Renderer targetRenderer;
    private readonly List<Vector3> layout = new List<Vector3>();

    private float sessionStartTime;
    private float repStartTime;
    private readonly List<float> repTimes = new List<float>();
    private readonly List<float> reachDistances = new List<float>();

    // Normalised target spots (x = left/right, y = down/up)
    private static readonly Vector2[] Pattern =
    {
        new Vector2(0f, 0f), new Vector2(-1f, 0f), new Vector2(1f, 0f),
        new Vector2(-0.6f, 1f), new Vector2(0.6f, 1f),
        new Vector2(-0.6f, -1f), new Vector2(0.6f, -1f),
        new Vector2(0f, 1f), new Vector2(-1f, 0.5f), new Vector2(1f, -0.5f)
    };

    private void Start()
    {
        if (useMenuSettings)
        {
            targetRepetitions = RehabSettings.TargetReachReps;
            reachDistance = RehabSettings.TargetReachTolerance;
            handMode = (HandMode)(int)RehabSettings.Hand;
        }

        if (headTransform == null && Camera.main != null)
            headTransform = Camera.main.transform;

        originalScale = transform.localScale;
        targetRenderer = GetComponent<Renderer>();

        if (exercisePanel != null) exercisePanel.SetActive(true);
        if (completionPanel != null) completionPanel.SetActive(false);

        if (progressBar != null)
        {
            progressBar.minValue = 0;
            progressBar.maxValue = targetRepetitions;
            progressBar.value = 0;
        }

        if (exerciseText != null)
        {
            exerciseText.text = "Exercise: TARGET REACH\nTouch the glowing ball with your " +
                                HandLabel() + ".\nLevel: " + RehabSettings.DifficultyName;
        }

        UpdateUI();
        SetTargetVisible(false);
        StartCoroutine(BeginAfterCountdown());
    }

    private IEnumerator BeginAfterCountdown()
    {
        for (int i = countdownSeconds; i > 0; i--)
        {
            SetStatus("Get ready... " + i);
            yield return new WaitForSeconds(1f);
        }

        BuildLayout();
        currentTarget = 0;
        if (layout.Count > 0)
            transform.position = layout[0];
        targetPosition = transform.position;

        SetTargetVisible(true);
        exerciseRunning = true;
        sessionStartTime = Time.time;
        repStartTime = Time.time;
        SetStatus("Reach the highlighted target");
    }

    private void BuildLayout()
    {
        layout.Clear();

        if (placeTargetsAroundPlayer && headTransform != null)
        {
            Vector3 forward = headTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            // Shift towards the working shoulder when only one hand is used
            float side = handMode == HandMode.Right ? 0.15f : handMode == HandMode.Left ? -0.15f : 0f;

            Vector3 centre = headTransform.position
                             + forward * RehabSettings.TargetReachForward
                             + Vector3.down * heightBelowEyes
                             + right * side;

            float halfW = RehabSettings.TargetReachHalfWidth;
            float halfH = RehabSettings.TargetReachHalfHeight;
            if (handMode != HandMode.Both) halfW *= 0.7f;

            for (int i = 0; i < targetRepetitions; i++)
            {
                Vector2 p = Pattern[i % Pattern.Length];
                layout.Add(centre + right * (p.x * halfW) + Vector3.up * (p.y * halfH));
            }
        }
        else if (targetPositions != null)
        {
            foreach (var t in targetPositions)
                if (t != null) layout.Add(t.position);
        }
    }

    private void Update()
    {
        if (exerciseCompleted) return;

        AnimateTarget();

        if (!exerciseRunning) return;

        if (movingToNextTarget)
        {
            MoveTarget();
            return;
        }

        Transform hand = HandInReach();
        if (hand != null)
            CompleteReach(hand);
    }

    private Transform HandInReach()
    {
        bool useRight = handMode != HandMode.Left;
        bool useLeft = handMode != HandMode.Right;

        if (useRight)
        {
            if (IsTouching(rightHandPoint)) return rightHandPoint;
            if (IsTouching(controllerTransform)) return controllerTransform;
        }

        if (useLeft)
        {
            if (IsTouching(leftHandPoint)) return leftHandPoint;
            if (IsTouching(leftControllerTransform)) return leftControllerTransform;
        }

        return null;
    }

    // Only counts a tracker that is currently active (hands OR controllers)
    private bool IsTouching(Transform tracker)
    {
        return tracker != null && tracker.gameObject.activeInHierarchy &&
               Vector3.Distance(tracker.position, transform.position) <= reachDistance;
    }

    private void AnimateTarget()
    {
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        transform.localScale = originalScale * pulse;
    }

    private void CompleteReach(Transform hand)
    {
        if (exerciseCompleted) return;

        repetitions++;
        repTimes.Add(Time.time - repStartTime);

        if (headTransform != null)
        {
            Vector3 d = hand.position - headTransform.position;
            d.y = 0f;
            reachDistances.Add(d.magnitude);
        }

        PlayerPrefs.SetInt("TargetReachRepetitions", repetitions);
        PlayerPrefs.Save();
        UpdateUI();

        if (repetitions >= targetRepetitions)
        {
            FinishExercise();
            return;
        }

        SetStatus("Great! Next target...");
        MoveToNextTarget();
    }

    private void FinishExercise()
    {
        exerciseCompleted = true;
        exerciseRunning = false;
        repetitions = targetRepetitions;

        float total = Time.time - sessionStartTime;
        float avg = Average(repTimes);
        float best = Min(repTimes);
        float maxReach = Max(reachDistances);

        PlayerPrefs.SetInt("TargetReachRepetitions", targetRepetitions);
        PlayerPrefs.SetInt("TargetReachCompleted", 1);
        PlayerPrefs.Save();

        RehabSessionHistory.Add(new RehabSessionRecord
        {
            exercise = "Target Reach",
            difficulty = RehabSettings.DifficultyName,
            hand = HandName(),
            reps = repetitions,
            attempts = repetitions,
            totalSeconds = total,
            avgSecondsPerRep = avg,
            bestRepSeconds = best,
            maxReachMetres = maxReach,
            avgReachMetres = Average(reachDistances),
            accuracyPercent = 100f
        });

        if (completionResultText != null)
        {
            completionResultText.text = string.Format(
                "Reps: {0}   |   Time: {1:F0}s\nAvg per rep: {2:F1}s   |   Fastest: {3:F1}s\nFurthest reach: {4:F0} cm",
                repetitions, total, avg, best, maxReach * 100f);
        }

        SetStatus("Exercise Complete!");
        SetTargetVisible(false);
        if (exercisePanel != null) exercisePanel.SetActive(false);
        if (completionPanel != null) completionPanel.SetActive(true);
    }

    private void MoveToNextTarget()
    {
        if (layout.Count == 0) return;
        currentTarget = (currentTarget + 1) % layout.Count;
        targetPosition = layout[currentTarget];
        movingToNextTarget = true;
    }

    private void MoveTarget()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            transform.position = targetPosition;
            movingToNextTarget = false;
            repStartTime = Time.time;
            SetStatus("Reach the highlighted target");
        }
    }

    private void UpdateUI()
    {
        if (repetitionText != null)
            repetitionText.text = "REPETITIONS\n" + repetitions + " / " + targetRepetitions;

        if (progressBar != null)
            progressBar.value = repetitions;
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    private void SetTargetVisible(bool visible)
    {
        if (targetRenderer != null) targetRenderer.enabled = visible;
    }

    private string HandLabel()
    {
        return handMode == HandMode.Right ? "right hand" : handMode == HandMode.Left ? "left hand" : "either hand";
    }

    private string HandName()
    {
        return handMode.ToString();
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

    private static float Max(List<float> values)
    {
        if (values.Count == 0) return 0f;
        float m = values[0];
        foreach (float v in values) if (v > m) m = v;
        return m;
    }
}
