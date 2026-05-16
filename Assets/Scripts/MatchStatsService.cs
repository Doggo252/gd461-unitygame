using UnityEngine;

// Local persistent W/L tally vs the AI. Backed by PlayerPrefs.
// Static — no scene reference needed; readable from any scene.
public static class MatchStatsService
{
    const string KEY_WINS   = "TankRoyale.Wins";
    const string KEY_LOSSES = "TankRoyale.Losses";
    const string KEY_DRAWS  = "TankRoyale.Draws";

    public static int Wins   => PlayerPrefs.GetInt(KEY_WINS,   0);
    public static int Losses => PlayerPrefs.GetInt(KEY_LOSSES, 0);
    public static int Draws  => PlayerPrefs.GetInt(KEY_DRAWS,  0);

    public static void RecordWin()
    {
        PlayerPrefs.SetInt(KEY_WINS, Wins + 1);
        PlayerPrefs.Save();
    }

    public static void RecordLoss()
    {
        PlayerPrefs.SetInt(KEY_LOSSES, Losses + 1);
        PlayerPrefs.Save();
    }

    public static void RecordDraw()
    {
        PlayerPrefs.SetInt(KEY_DRAWS, Draws + 1);
        PlayerPrefs.Save();
    }

    public static void Reset()
    {
        PlayerPrefs.DeleteKey(KEY_WINS);
        PlayerPrefs.DeleteKey(KEY_LOSSES);
        PlayerPrefs.DeleteKey(KEY_DRAWS);
        PlayerPrefs.Save();
    }
}
