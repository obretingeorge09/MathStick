using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class Fader : MonoBehaviour
{
    public Image image = null;
    public GameObject panel = null;
    string currentFadeRequestId = null;
    float secInFade = 0f;

    // Awake instead of OnEnable so this works when placed on an always-active
    // manager object while the fader panel itself is toggled separately.
    void Awake()
    {
        Messenger.AddListener<float, string>(Message.OnStartFadeToTransparent, (nrSec, fadeId) => { secInFade = nrSec; currentFadeRequestId = fadeId; StartCoroutine(FadeCo(true)); });
        Messenger.AddListener<float, string>(Message.OnStartFadeToOpaque, (nrSec, fadeId) => { secInFade = nrSec; currentFadeRequestId = fadeId; StartCoroutine(FadeCo(false)); });
    }

    IEnumerator FadeCo(bool toTransparent)
    {
        if (toTransparent == false)
        {
            panel.SetActive(true);
        }

        float timeSoFar = 0f;
        Color transparentBlack = Color.black;
        transparentBlack.a = 0f;
        float ratio = 0f;

        while (true)
        {
            yield return null;
            timeSoFar += Time.unscaledDeltaTime;
            if (timeSoFar > secInFade)
            {
                break;
            }

            if (toTransparent == true)
            {
                ratio = 1f - timeSoFar / secInFade;
            }
            else
            {
                ratio = timeSoFar / secInFade;
            }
            image.color = Color.Lerp(transparentBlack, Color.black, ratio);
        }

        if (toTransparent == true)
        {
            image.color = transparentBlack;
        }
        else
        {
            image.color = Color.black;
        }

        if (toTransparent == true)
        {
            Messenger.Broadcast<string>(Message.OnEndFadeToTransparent, currentFadeRequestId);
            panel.SetActive(false);
        }
        else
        {
            Messenger.Broadcast<string>(Message.OnEndFadeToOpaque, currentFadeRequestId);
        }
    }
}
