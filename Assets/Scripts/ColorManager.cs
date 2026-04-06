using UnityEngine;
using System.Collections;

public class ColorManager : MonoBehaviour
{
    public Color ActiveNumberColor = Color.white;
    public Color PossibleNumberColor = Color.white;
    public Color ImpossibleNumberColor = Color.white;
    public Color ActiveInnerColor = new Color(1f, 1f, 1f, 0.5f);
    public Color PossibleImpossibleInnerColor = new Color(1f, 1f, 1f, 0.2f);
    public Color BackgroundColor = Color.white;
    public Color WinGUIColor = Color.white;
    public Color LoseGUIColor = Color.white;

    void Awake()
    {
        Messenger.AddReceiverListener<Color>(ReceiveMessage.ReceiveActiveNumberColor, () => { return ActiveNumberColor; });
        Messenger.AddReceiverListener<Color>(ReceiveMessage.ReceivePossibleNumberColor, () => { return PossibleNumberColor; });
        Messenger.AddReceiverListener<Color>(ReceiveMessage.ReceiveImpossibleNumberColor, () => { return ImpossibleNumberColor; });
        Messenger.AddReceiverListener<Color>(ReceiveMessage.ReceiveActiveInnerColor, () => { return ActiveInnerColor; });
        Messenger.AddReceiverListener<Color>(ReceiveMessage.ReceivePossibleImpossibleInnerColor, () => { return PossibleImpossibleInnerColor; });
        Messenger.AddReceiverListener<Color>(ReceiveMessage.ReceiveWinGUIColor, () => { return WinGUIColor; });
        Messenger.AddReceiverListener<Color>(ReceiveMessage.ReceiveLoseGUIColor, () => { return LoseGUIColor; });
    }

    void Start()
    {
        Camera.main.backgroundColor = BackgroundColor;
    }
}
