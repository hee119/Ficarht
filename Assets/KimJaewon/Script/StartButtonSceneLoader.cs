using UnityEngine;
using UnityEngine.SceneManagement;

public class StartButtonSceneLoader : MonoBehaviour
{
    [Header("이동할 씬 이름")]
    public string sceneName = "Forest";

    public void LoadSelectedScene()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[StartButtonSceneLoader] 이동할 씬 이름이 비어 있습니다.");
            return;
        }

        if (
            CardSystemManager.Instance != null &&
            !CardSystemManager.Instance.CanMoveToBattleScene()
        )
        {
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
