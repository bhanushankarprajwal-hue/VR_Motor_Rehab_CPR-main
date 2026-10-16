using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Makes grabbing reliable for BOTH controllers and tracked hands.
/// Runs in every scene automatically (no setup needed).
///
/// Grab happens when the hand/controller is close to a grabbable object
/// (or its ray is pointing at it) AND the user is "grabbing":
///   - Controller: grip or trigger pressed
///   - Hand: closed hand (fist / grab pose) or pinch
///   - Simulator (Editor only): holding G, T, K or P also counts
/// Uses the XRI manual-interaction API, so the normal grab events
/// (selectEntered / selectExited) still fire for the exercise scripts.
/// </summary>
public class HandGrabAssist : MonoBehaviour
{
    public float nearReach = 0.15f;        // metres from hand to object surface
    public float closedDistance = 0.07f;   // fingertip-to-palm, closed hand
    public float openDistance = 0.085f;    // fingertip-to-palm, open again

    class Entry
    {
        public NearFarInteractor interactor;
        public bool isHand;
        public bool closed;
        public bool wasButton, wasClosed, wasActive;
        public int source;
        public bool manual;
    }

    readonly List<Entry> entries = new List<Entry>();
    readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
    XRGrabInteractable[] grabbables = new XRGrabInteractable[0];
    float nextScan;
    bool prevKeyGrab;
    float lastAvg;
    bool lastTracked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<HandGrabAssist>() != null) return;
        var go = new GameObject("HandGrabAssist");
        DontDestroyOnLoad(go);
        go.AddComponent<HandGrabAssist>();
    }

    void Update()
    {
        if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 1f; Scan(); }

        XRHandSubsystem sub = GetSubsystem();
        bool keyGrab = KeyboardGrabHeld();
        bool keyDown = keyGrab && !prevKeyGrab;
        prevKeyGrab = keyGrab;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (e.interactor == null) { entries.RemoveAt(i); continue; }

            bool active = e.interactor.isActiveAndEnabled;
            if (active != e.wasActive)
            {
                e.wasActive = active;
                Debug.Log($"[GrabAssist] {Label(e.interactor)} is now {(active ? "ACTIVE" : "inactive")}.");
            }
            if (!active)
            {
                if (e.manual) { e.interactor.EndManualInteraction(); e.manual = false; }
                e.wasButton = e.wasClosed = false;
                continue;
            }

            // Separate sources so one stuck source can't block the others
            bool button = e.isHand ? ReadSelect(e.interactor) : ReadButton(e.interactor);
            bool closed = false;
            if (e.isHand)
            {
                e.closed = IsHandClosed(sub, e.interactor.handedness == InteractorHandedness.Left, e.closed, out float avg);
                closed = e.closed;
            }
            if (e.isHand && (button != e.wasButton || closed != e.wasClosed))
                Debug.Log($"[GrabAssist] {Label(e.interactor)} pinch={button} closedHand={closed} (finger-palm {lastAvg * 100f:0.0} cm, tracked={lastTracked}).");

            bool buttonDown = button && !e.wasButton;
            bool closedDown = closed && !e.wasClosed;
            e.wasButton = button; e.wasClosed = closed;

            if (!e.manual && !e.interactor.hasSelection && (keyDown || buttonDown || closedDown))
            {
                e.source = keyDown ? 0 : (buttonDown ? 1 : 2);
                TryGrab(e);
            }
            else if (!e.manual && e.interactor.hasSelection && (keyDown || buttonDown || closedDown))
            {
                Debug.Log($"[GrabAssist] {Label(e.interactor)} already holding '{e.interactor.interactablesSelected[0].transform.name}'.");
            }
            else if (e.manual)
            {
                bool held = e.source == 0 ? keyGrab : (e.source == 1 ? button : closed);
                if (!held || !e.interactor.isPerformingManualInteraction)
                {
                    if (e.interactor.isPerformingManualInteraction) e.interactor.EndManualInteraction();
                    e.manual = false;
                }
            }
        }
    }

    static bool ReadSelect(NearFarInteractor nf) => nf.selectInput != null && nf.selectInput.ReadIsPerformed();

    void TryGrab(Entry e)
    {
        var nf = e.interactor;
        Vector3 handPos = nf.attachTransform != null ? nf.attachTransform.position : nf.transform.position;

        XRGrabInteractable best = null;
        float bestDist = float.MaxValue;
        foreach (var g in grabbables)
        {
            if (g == null || !g.isActiveAndEnabled) continue;
            if (g.isSelected && !g.selectMode.Equals(UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Multiple)) continue;
            float d = DistanceToGrabbable(g, handPos);
            if (d < bestDist) { bestDist = d; best = g; }
        }

        bool hovered = false;
        if (best == null || bestDist > nearReach)
        {
            // Not close enough - allow grabbing what the ray is pointing at
            foreach (var h in nf.interactablesHovered)
                if (h is XRGrabInteractable hg && !hg.isSelected) { best = hg; hovered = true; break; }
        }

        if (best == null || (!hovered && bestDist > nearReach))
        {
            if (best != null)
                Debug.Log($"[GrabAssist] {Label(nf)} grab pressed, but '{best.name}' is {bestDist * 100f:0} cm away (need {nearReach * 100f:0} cm or point the ray at it).");
            return;
        }

        nf.StartManualInteraction((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)best);
        e.manual = nf.isPerformingManualInteraction;
        Debug.Log($"[GrabAssist] {Label(nf)} {(e.manual ? "grabbed" : "could NOT grab")} '{best.name}' ({(hovered ? "ray" : (bestDist * 100f).ToString("0") + " cm")}).");
    }

    static float DistanceToGrabbable(XRGrabInteractable g, Vector3 p)
    {
        float best = Vector3.Distance(g.transform.position, p);
        var cols = g.colliders;
        if (cols != null && cols.Count > 0)
        {
            foreach (var c in cols)
            {
                if (c == null || !c.enabled) continue;
                bool ok = c is BoxCollider || c is SphereCollider || c is CapsuleCollider || (c is MeshCollider mc && mc.convex);
                float d = ok ? Vector3.Distance(c.ClosestPoint(p), p) : Vector3.Distance(c.bounds.ClosestPoint(p), p);
                if (d < best) best = d;
            }
        }
        return best;
    }

    void Scan()
    {
        grabbables = FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);
        var found = FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var nf in found)
        {
            bool known = false;
            foreach (var e in entries) if (e.interactor == nf) { known = true; break; }
            if (known) continue;
            var entry = new Entry { interactor = nf, isHand = IsHandInteractor(nf.transform) };
            entries.Add(entry);
            Debug.Log($"[GrabAssist] Watching {Label(nf)} ({(entry.isHand ? "hand" : "controller")}).");
        }
    }

    static string Label(Component c) => (c.transform.parent != null ? c.transform.parent.name + "/" : "") + c.name;

    static bool ReadButton(NearFarInteractor nf)
    {
        bool v = false;
        if (nf.selectInput != null) v |= nf.selectInput.ReadIsPerformed();
        if (nf.activateInput != null) v |= nf.activateInput.ReadIsPerformed();
        return v;
    }

    static bool KeyboardGrabHeld()
    {
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && (kb.gKey.isPressed || kb.tKey.isPressed || kb.kKey.isPressed || kb.pKey.isPressed))
            return true;
#endif
        return false;
    }

    static bool IsHandInteractor(Transform t)
    {
        bool hand = false;
        for (var p = t; p != null; p = p.parent)
        {
            string n = p.name;
            if (n.Contains("Controller")) return false;
            if (n.Contains("Hand") && !n.Contains("Hands")) hand = true;
        }
        return hand;
    }

    XRHandSubsystem GetSubsystem()
    {
        subsystems.Clear();
        SubsystemManager.GetSubsystems(subsystems);
        foreach (var s in subsystems) if (s.running) return s;
        return null;
    }

    bool IsHandClosed(XRHandSubsystem sub, bool left, bool wasClosed, out float avg)
    {
        avg = 0f; lastAvg = 0f; lastTracked = false;
        if (sub == null) return false;
        XRHand hand = left ? sub.leftHand : sub.rightHand;
        lastTracked = hand.isTracked;
        if (!hand.isTracked) return false;
        if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palm)) return false;

        float total = 0f; int count = 0;
        AddTip(hand, XRHandJointID.MiddleTip, palm, ref total, ref count);
        AddTip(hand, XRHandJointID.RingTip, palm, ref total, ref count);
        AddTip(hand, XRHandJointID.LittleTip, palm, ref total, ref count);
        if (count == 0) return false;
        avg = total / count;
        lastAvg = avg;
        return wasClosed ? avg < openDistance : avg < closedDistance;
    }

    static void AddTip(XRHand hand, XRHandJointID id, Pose palm, ref float total, ref int count)
    {
        if (hand.GetJoint(id).TryGetPose(out Pose tip)) { total += Vector3.Distance(tip.position, palm.position); count++; }
    }
}
