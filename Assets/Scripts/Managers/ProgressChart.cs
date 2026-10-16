using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple bar chart of recent rehab sessions (seconds per rep), built with uGUI at runtime.
/// One series per chart, so no legend: the title names it. Lower bars = faster = improving.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ProgressChart : MonoBehaviour
{
    [Tooltip("Exercise name exactly as saved in history (e.g. Target Reach)")]
    public string exercise = "Target Reach";
    public string title = "TARGET REACH";
    [Tooltip("How many recent sessions to show")]
    public int maxSessions = 7;

    [Header("Colours")]
    public Color barColor = new Color32(0x2a, 0x78, 0xd6, 0xff);
    public Color latestBarColor = new Color32(0x1f, 0x5f, 0xb0, 0xff);
    public Color textPrimary = new Color32(0x0b, 0x0b, 0x0b, 0xff);
    public Color textSecondary = new Color32(0x52, 0x51, 0x4e, 0xff);
    public Color gridColor = new Color(0f, 0f, 0f, 0.12f);
    public Color panelColor = new Color(1f, 1f, 1f, 0.85f);

    private RectTransform root;

    private void Start()
    {
        Build();
    }

    public void Build()
    {
        root = (RectTransform)transform;
        for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);

        float w = root.rect.width, h = root.rect.height;
        Img(root, "Panel", Vector2.zero, new Vector2(w, h), panelColor);

        Txt(root, "Title", title, new Vector2(-w / 2 + 16, h / 2 - 22), new Vector2(w * 0.6f, 34), 25, textPrimary, TextAlignmentOptions.Left, FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
        Txt(root, "Subtitle", "Seconds per rep, last " + maxSessions + " sessions (lower is better)", new Vector2(-w / 2 + 16, h / 2 - 52), new Vector2(w - 32, 24), 18, textSecondary, TextAlignmentOptions.Left, FontStyles.Normal);

        var all = RehabSessionHistory.ForExercise(exercise);
        int start = Mathf.Max(0, all.Count - maxSessions);
        var data = all.GetRange(start, all.Count - start);

        float left = -w / 2 + 64, right = w / 2 - 20, top = h / 2 - 84, bottom = -h / 2 + 38;
        float plotW = right - left, plotH = top - bottom;

        if (data.Count == 0)
        {
            Txt(root, "Empty", "No sessions yet.\nComplete the exercise to see your progress here.", new Vector2(-w / 2 + 20, (top + bottom) / 2), new Vector2(w - 40, 70), 20, textSecondary, TextAlignmentOptions.Center, FontStyles.Normal);
            return;
        }

        float maxVal = 0f;
        foreach (var d in data) maxVal = Mathf.Max(maxVal, d.avgSecondsPerRep);
        float niceMax = NiceCeiling(Mathf.Max(maxVal, 1f));

        // recessive gridlines + axis labels (0, half, max)
        for (int g = 0; g <= 2; g++)
        {
            float v = niceMax * g / 2f;
            float y = bottom + plotH * g / 2f;
            Img(root, "Grid" + g, new Vector2((left + right) / 2, y), new Vector2(plotW, g == 0 ? 2 : 1), g == 0 ? new Color(0, 0, 0, 0.35f) : gridColor);
            Txt(root, "Axis" + g, v.ToString("0.#") + "s", new Vector2(left - 60, y), new Vector2(54, 22), 17, textSecondary, TextAlignmentOptions.Right, FontStyles.Normal);
        }

        // bars: thin, anchored to the baseline, clear gaps between them
        float slot = plotW / maxSessions;
        float barW = Mathf.Min(42f, slot * 0.6f);
        for (int i = 0; i < data.Count; i++)
        {
            var d = data[i];
            float x = left + slot * (i + 0.5f);
            float bh = Mathf.Max(3f, plotH * (d.avgSecondsPerRep / niceMax));
            bool latest = i == data.Count - 1;
            Img(root, "Bar" + i, new Vector2(x, bottom + bh / 2f), new Vector2(barW, bh), latest ? latestBarColor : barColor);

            string date = !string.IsNullOrEmpty(d.date) && d.date.Length >= 6 ? d.date.Substring(0, 6) : ("#" + (i + 1));
            Txt(root, "Date" + i, date, new Vector2(x - slot / 2, bottom - 20), new Vector2(slot, 22), 16, textSecondary, TextAlignmentOptions.Center, FontStyles.Normal);

            // selective direct label: only the newest session carries its value
            if (latest)
                Txt(root, "Value", d.avgSecondsPerRep.ToString("0.0") + "s", new Vector2(x - 45, bottom + bh + 14), new Vector2(90, 24), 20, textPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
        }

        // short trend note in plain words
        if (data.Count >= 2)
        {
            float first = data[0].avgSecondsPerRep, last = data[data.Count - 1].avgSecondsPerRep;
            string since = !string.IsNullOrEmpty(data[0].date) && data[0].date.Length >= 6 ? data[0].date.Substring(0, 6) : "first session";
            string note = last < first ? string.Format("Faster by {0:0.0}s per rep since {1}", first - last, since)
                        : last > first ? string.Format("Slower by {0:0.0}s per rep since {1}", last - first, since)
                        : "Same speed as the first session shown";
            Txt(root, "Trend", note, new Vector2(w / 2 - 16 - w * 0.38f, h / 2 - 22), new Vector2(w * 0.38f, 26), 15, textSecondary, TextAlignmentOptions.Right, FontStyles.Italic);
        }
    }

    private static float NiceCeiling(float v)
    {
        float[] steps = { 1, 2, 5, 10, 15, 20, 30, 45, 60, 90, 120, 180, 300 };
        foreach (var s in steps) if (v <= s) return s;
        return Mathf.Ceil(v / 60f) * 60f;
    }

    private RectTransform MakeRect(Transform parent, string name, Vector2 centre, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = centre;
        rt.sizeDelta = size;
        return rt;
    }

    private Image Img(Transform parent, string name, Vector2 centre, Vector2 size, Color c)
    {
        var img = MakeRect(parent, name, centre, size).gameObject.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    // leftCentre = left edge (x) and vertical centre (y) of the text box
    private TMP_Text Txt(Transform parent, string name, string text, Vector2 leftCentre, Vector2 size, float fontSize, Color c, TextAlignmentOptions align, FontStyles style)
    {
        var rt = MakeRect(parent, name, leftCentre + new Vector2(size.x / 2f, 0), size);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = c;
        t.alignment = align;
        t.fontStyle = style;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }
}
