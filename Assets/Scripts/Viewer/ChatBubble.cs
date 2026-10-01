using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ChatBubble : MonoBehaviour
{
    private const float FadeTime = 0.5f;

    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private Image background;

    private ViewerCharacter _owner;

    public void Setup(ViewerCharacter owner, string nickname, string content, float duration)
    {
        _owner = owner;

        if (contentText != null)
            contentText.text = WrapText(content);

        StartCoroutine(FadeOutAfter(duration));
    }

    private static string WrapText(string input, int maxCharsPerLine = 10)
    {
        var sb = new StringBuilder(input.Length + input.Length / maxCharsPerLine + 1);

        int count = 0;
        foreach (char c in input)
        {
            sb.Append(c);
            if (++count >= maxCharsPerLine && c != '\n')
            {
                sb.Append('\n');
                count = 0;
            }
        }

        return sb.ToString();
    }

    private IEnumerator FadeOutAfter(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, duration - FadeTime));

        if (!TryGetComponent(out CanvasGroup group))
            group = gameObject.AddComponent<CanvasGroup>();

        for (float elapsed = 0f; elapsed < FadeTime; elapsed += Time.deltaTime)
        {
            group.alpha = 1f - elapsed / FadeTime;
            yield return null;
        }

        group.alpha = 1f;
        group.blocksRaycasts = false;
        group.interactable = false;
        group.ignoreParentGroups = false;
    }
}
