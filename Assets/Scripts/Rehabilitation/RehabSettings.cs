using UnityEngine;

public enum RehabDifficulty { Easy = 0, Medium = 1, Hard = 2 }
public enum RehabHand { Right = 0, Left = 1, Both = 2 }

/// <summary>
/// Stores the patient's rehab choices (difficulty and hand) and turns them
/// into exercise numbers. Saved on the device so choices are remembered.
/// </summary>
public static class RehabSettings
{
    private const string DifficultyKey = "Rehab_Difficulty";
    private const string HandKey = "Rehab_Hand";

    public static RehabDifficulty Difficulty
    {
        get { return (RehabDifficulty)PlayerPrefs.GetInt(DifficultyKey, (int)RehabDifficulty.Medium); }
        set { PlayerPrefs.SetInt(DifficultyKey, (int)value); PlayerPrefs.Save(); }
    }

    public static RehabHand Hand
    {
        get { return (RehabHand)PlayerPrefs.GetInt(HandKey, (int)RehabHand.Both); }
        set { PlayerPrefs.SetInt(HandKey, (int)value); PlayerPrefs.Save(); }
    }

    public static string DifficultyName { get { return Difficulty.ToString(); } }

    public static string HandName
    {
        get
        {
            switch (Hand)
            {
                case RehabHand.Right: return "Right";
                case RehabHand.Left: return "Left";
                default: return "Both";
            }
        }
    }

    public static string HandInstruction
    {
        get
        {
            switch (Hand)
            {
                case RehabHand.Right: return "right hand";
                case RehabHand.Left: return "left hand";
                default: return "either hand";
            }
        }
    }

    // ---------------- TARGET REACH ----------------
    public static int TargetReachReps { get { return Pick(5, 10, 15); } }
    /// <summary>How close (m) the hand must get to the target centre.</summary>
    public static float TargetReachTolerance { get { return Pick(0.20f, 0.15f, 0.10f); } }
    /// <summary>How far in front of the head (m) the target area sits.</summary>
    public static float TargetReachForward { get { return Pick(0.38f, 0.48f, 0.56f); } }
    /// <summary>Half width (m) of the target area, left to right.</summary>
    public static float TargetReachHalfWidth { get { return Pick(0.18f, 0.28f, 0.38f); } }
    /// <summary>Half height (m) of the target area, down to up.</summary>
    public static float TargetReachHalfHeight { get { return Pick(0.10f, 0.18f, 0.26f); } }

    // ---------------- OBJECT PLACEMENT ----------------
    public static int PlacementReps { get { return Pick(5, 10, 15); } }
    /// <summary>Sideways distance (m) from the start spot to the target zone.</summary>
    public static float PlacementOffset { get { return Pick(0.28f, 0.40f, 0.52f); } }
    /// <summary>How far (m) the object must travel for the rep to count.</summary>
    public static float PlacementMoveDistance { get { return PlacementOffset * 0.7f; } }
    /// <summary>How close (m) to the zone centre the object must be dropped.</summary>
    public static float PlacementTolerance { get { return Pick(0.14f, 0.10f, 0.07f); } }

    private static int Pick(int easy, int medium, int hard)
    {
        return Difficulty == RehabDifficulty.Easy ? easy : Difficulty == RehabDifficulty.Hard ? hard : medium;
    }

    private static float Pick(float easy, float medium, float hard)
    {
        return Difficulty == RehabDifficulty.Easy ? easy : Difficulty == RehabDifficulty.Hard ? hard : medium;
    }
}
