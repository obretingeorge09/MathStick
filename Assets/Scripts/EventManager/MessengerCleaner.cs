using UnityEngine;
using System.Collections;

//This manager will ensure that the messenger's eventTable will be cleaned up upon loading of a new level.
public sealed class MessengerCleaner : MonoBehaviour
{
	//Clean up eventTable every time a new level loads.
	public void OnDisable ()
	{
		Messenger.Cleanup ();
	}
}
