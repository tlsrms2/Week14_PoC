using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class HyperlinkHandler : MonoBehaviour, IPointerClickHandler
{
    public TextMeshProUGUI tmpText;

    public void OnPointerClick(PointerEventData eventData)
    {
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(tmpText, eventData.position, null);
        if (linkIndex != -1)
        {
            TMP_LinkInfo linkInfo = tmpText.textInfo.linkInfo[linkIndex];
            string url = linkInfo.GetLinkID();
            Application.OpenURL(url);
        }
    }
}