using System;
using System.Globalization;
using System.IO;
using System.Text;
using MyGame.Core;
using UnityEngine;

/// <summary>
/// TEMPORAL: mide cuanto le cuesta al movil decidir cada jugada del bot (sobre todo Monte Carlo).
/// Guarda cada decision en bot_perf.csv (Application.persistentDataPath), la escribe en el log y la muestra en pantalla.
/// Quitar BotPerfOverlay y BotPerf cuando ya no haga falta.
/// </summary>
public static class BotPerf
{
    public const int KeepLast = 20;

    public static int Decisions;
    public static BotDecision Last;
    public static float LastSlowestFrameMs;
    public static double[] Recent = new double[KeepLast];
    public static double MaxMs;
    public static double TotalMs;

    public static string CsvPath => Path.Combine(Application.persistentDataPath, "bot_perf.csv");

    public static void Record(BotDecision decision, float slowestFrameMs)
    {
        if (decision == null) return;

        Last = decision;
        LastSlowestFrameMs = slowestFrameMs;
        Recent[Decisions % KeepLast] = decision.ElapsedMs;
        Decisions++;
        TotalMs += decision.ElapsedMs;
        if (decision.ElapsedMs > MaxMs) MaxMs = decision.ElapsedMs;

        string line = string.Format(CultureInfo.InvariantCulture, "{0:o},{1},{2},{3},{4},{5},{6:F1},{7:F1},{8}",
            DateTime.UtcNow, SystemInfo.deviceModel.Replace(',', ' '), decision.ProfileName ?? decision.Level.ToString(), decision.LegalCells, decision.Finalists,
            decision.Rollouts, decision.ElapsedMs, slowestFrameMs, decision.HasMove);

        Debug.Log("[BotPerf] " + line);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        try
        {
            if (!File.Exists(CsvPath)) File.AppendAllText(CsvPath, "utc,device,level,legalCells,finalists,rollouts,decisionMs,slowestFrameMs,hasMove\n");
            File.AppendAllText(CsvPath, line + "\n");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BotPerf] No se pudo escribir el CSV: " + e.Message);
        }
#endif
    }

    public static double RecentAverage()
    {
        int n = Math.Min(Decisions, KeepLast);
        if (n == 0) return 0;
        double sum = 0;
        for (int i = 0; i < n; i++) sum += Recent[i];
        return sum / n;
    }
}

public class BotPerfOverlay : MonoBehaviour
{
    private bool expanded = true;
    private float smoothedFrameMs = 16f;
    private GUIStyle style;
    private GUIStyle buttonStyle;

    private void Update()
    {
        smoothedFrameMs = Mathf.Lerp(smoothedFrameMs, Time.unscaledDeltaTime * 1000f, 0.05f);
    }

    private void OnGUI()
    {
        // Escala por densidad de pantalla para que se lea en el movil
        float scale = Mathf.Clamp(Screen.dpi > 0 ? Screen.dpi / 160f : 1f, 1f, 3.5f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 12, wordWrap = false };
            style.normal.textColor = Color.white;
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12 };
        }

        float x = 6f, y = 6f;
        if (GUI.Button(new Rect(x, y, 110f, 24f), expanded ? "Bot perf [-]" : "Bot perf [+]", buttonStyle)) expanded = !expanded;
        if (!expanded) return;

        var sb = new StringBuilder();
        sb.AppendLine($"{SystemInfo.deviceModel}  {SystemInfo.processorCount} nucleos @ {SystemInfo.processorFrequency} MHz");
        sb.AppendLine($"Fotograma: {smoothedFrameMs:F1} ms ({(smoothedFrameMs > 0 ? 1000f / smoothedFrameMs : 0):F0} fps)");

        BotDecision d = BotPerf.Last;
        if (d == null)
        {
            sb.Append("Aun no ha jugado ningun bot.");
        }
        else
        {
            sb.AppendLine($"Dificultad: {d.ProfileName ?? d.Level.ToString()}   Decisiones: {BotPerf.Decisions}");
            sb.AppendLine($"Ultima: {d.ElapsedMs:F0} ms  | casillas legales {d.LegalCells}");
            if (d.Rollouts > 0)
                sb.AppendLine($"Monte Carlo: {d.Finalists} finalistas, {d.Rollouts} simulaciones ({(d.Rollouts > 0 ? d.ElapsedMs / d.Rollouts : 0):F1} ms c/u)");
            sb.AppendLine($"Media(ult. {BotPerf.KeepLast}): {BotPerf.RecentAverage():F0} ms   Max: {BotPerf.MaxMs:F0} ms");
            sb.Append($"Peor fotograma mientras pensaba: {BotPerf.LastSlowestFrameMs:F0} ms");
        }

        GUI.Box(new Rect(x, y + 28f, 430f, expanded ? 118f : 0f), sb.ToString(), style);
    }
}
