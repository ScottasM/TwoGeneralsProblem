using UnityEngine;

public class PauseGame : MonoBehaviour
{
    private AudioSource backgroundMusicAudioSource;

    private bool isPaused = false;


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            isPaused = !isPaused;
            if (isPaused)
            {
                PauseGameplay();
            }
            else
            {
                ResumeGameplay();
            }
        }
    }

    private void PauseGameplay()
    {
        Time.timeScale = 0;
        if (backgroundMusicAudioSource != null)
        {
            backgroundMusicAudioSource.Pause();
        }
    }

    private void ResumeGameplay()
    {
        Time.timeScale = 1;
        if (backgroundMusicAudioSource != null)
        {
            backgroundMusicAudioSource.UnPause();
        }
    }

    private void OnGUI()
    {
        if (isPaused)
        {
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 40;
            labelStyle.alignment = TextAnchor.MiddleCenter;

            GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 - 50, 300, 100), "Game paused", labelStyle);
        }
    }
}
