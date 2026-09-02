using UnityEngine;
using DG.Tweening;
public class LightBar : MonoBehaviour
{
    public GameObject imgNode;
    Tween _delayTween;

    public void shoot()
    {
        _delayTween?.Kill();
        RectTransform rectTransform = imgNode.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, 0f);

        Sequence mySequence = DOTween.Sequence();

        float parentHeight = ((RectTransform)rectTransform.parent).rect.height;

        mySequence
            .Append(rectTransform.DOAnchorPosY(parentHeight, 0.1f))
            .Append(rectTransform.DOAnchorPosY(0f, 0.1f));
        _delayTween = mySequence;
    }
}
