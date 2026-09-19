using UnityEngine;
using UnityEngine.SceneManagement; // Required to switch scenes

public class MainMenuController : MonoBehaviour
{
    [Tooltip("The exact name of your Game Scene file")]
    public string GameSceneName = "SampleScene"; 

    public void PlayGame()
    {
        // This command loads the game scene
        SceneManager.LoadScene(GameSceneName);
    }

    public void QuitGame()
    {
        Debug.Log("Quit Game"); // Just for testing in Editor
        Application.Quit();
    }
}
