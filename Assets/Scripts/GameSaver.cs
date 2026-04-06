using UnityEngine;
using System.Collections;

public class GameSaver : MonoBehaviour
{
    public int Highscore = 0;
    public int CurrentScore = 0;

    void Awake()
    {
        Messenger.AddListener<int, int>(Message.SaveGame, (hs, cs) => { SaveValues(hs, cs); });
    }

    void Start()
    {
        bool save = false;

        if (PlayerPrefs.HasKey("Highscore"))
        {
            Highscore = PlayerPrefs.GetInt("Highscore");
        }
        else
        {
            Highscore = 0;
            PlayerPrefs.SetInt("Highscore", 0);
            save = true;
        }

        if (PlayerPrefs.HasKey("CurrentScore"))
        {
            CurrentScore = PlayerPrefs.GetInt("CurrentScore");
        }
        else
        {
            CurrentScore = 0;
            PlayerPrefs.SetInt("CurrentScore", 0);
            save = true;
        }

        if (save)
        {
            PlayerPrefs.Save();
        }

        Messenger.Broadcast<int, int>(Message.SetHighscoreAndCurrentScore, Highscore, CurrentScore);
    }

    void SaveValues(int hS, int curS)
    {
        Highscore = hS;
        CurrentScore = curS;
        PlayerPrefs.SetInt("Highscore", hS);
        PlayerPrefs.SetInt("CurrentScore", curS);
        PlayerPrefs.Save();
    }
}
