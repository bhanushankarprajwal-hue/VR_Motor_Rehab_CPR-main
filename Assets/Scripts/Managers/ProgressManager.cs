using UnityEngine;
using TMPro;

/// <summary>
/// Shows rehab progress: status, number of sessions and the last session's results.
/// </summary>
public class ProgressManager : MonoBehaviour
{
    public TMP_Text rehabProgressText;
    public TMP_Text cprProgressText;

    private void Start()
    {
        if (rehabProgressText != null)
        {
            rehabProgressText.enableAutoSizing = true;
            rehabProgressText.fontSizeMin = Mathf.Max(rehabProgressText.fontSizeMin, 20);
            rehabProgressText.fontSizeMax = Mathf.Max(rehabProgressText.fontSizeMax, 28);

            rehabProgressText.text = "<b><size=120%>MOTOR REHABILITATION</size></b>\n\n" +
                ExerciseBlock("TARGET REACH", "Target Reach", "TargetReachCompleted", "TargetReachRepetitions", true) +
                "\n" +
                ExerciseBlock("OBJECT PLACEMENT", "Object Placement", "ObjectPlacementCompleted", "ObjectPlacementRepetitions", false);
        }

        if (cprProgressText != null)
            cprProgressText.text = "CPR Training\n\nStatus: Not Completed";
    }

    private string ExerciseBlock(string title, string exercise, string completedKey, string repsKey, bool showReach)
    {
        var sessions = RehabSessionHistory.ForExercise(exercise);
        var last = sessions.Count > 0 ? sessions[sessions.Count - 1] : null;

        string status;
        if (PlayerPrefs.GetInt(completedKey, 0) == 1) status = "Completed";
        else if (PlayerPrefs.GetInt(repsKey, 0) > 0) status = "In Progress";
        else status = "Not Started";

        string text = "<b>" + title + "</b>\nStatus: " + status + "\nSessions completed: " + sessions.Count + "\n";

        if (last != null)
        {
            text += "Last: " + last.date + " (" + last.difficulty + ", " + last.hand + ")\n";
            text += string.Format("{0} reps  |  {1:F1}s per rep", last.reps, last.avgSecondsPerRep);
            if (showReach) text += string.Format("  |  reach {0:F0} cm", last.maxReachMetres * 100f);
            else text += string.Format("  |  accuracy {0:F0}%", last.accuracyPercent);
            text += "\n";
        }

        return text;
    }
}
