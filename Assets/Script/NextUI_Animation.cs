using UnityEngine;
using DG.Tweening;

public class NextUI_Animation : MonoBehaviour
{
    [Header("Targets")]
    public GameObject[] targetObjects;

    [Header("Fall Settings")]
    public float fallStartY = 500f;
    public float duration = 0.5f;
    public Ease ease = Ease.OutBack;

    private Vector3[] startPositions;

    [Header("NextUI")] 
    public GameObject nextUI;

    private void OnEnable()
    {
        PlayFallAnimation();
    }

    public void PlayFallAnimation()
    {
        if (targetObjects == null || targetObjects.Length == 0)
            return;

        startPositions = new Vector3[targetObjects.Length];

        Sequence sequence = DOTween.Sequence();

        for (int i = 0; i < targetObjects.Length; i++)
        {
            if (targetObjects[i] == null)
                continue;

            Transform target = targetObjects[i].transform;

            startPositions[i] = target.position;

            Vector3 fallStartPosition = startPositions[i];
            fallStartPosition.y = this.gameObject.transform.position.y + fallStartY;

            target.position = fallStartPosition;

            sequence.Append(
                target.DOMove(startPositions[i], duration)
                    .SetEase(ease)
            );
        }
    }

    public void Next_UI()
    {
        if (nextUI != null)
            nextUI.SetActive(true);

        gameObject.SetActive(false);
    }
}