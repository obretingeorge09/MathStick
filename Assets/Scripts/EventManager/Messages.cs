/*
 * Based on the advanced C# messenger by Ilya Suzdalnitski. V1.0 that is based on Rod Hyde's "CSharpMessenger" and Magnus Wolffelt's "CSharpMessenger Extended".
 */

//#define LOG_ALL_MESSAGES
//#define LOG_ADD_LISTENER
//#define LOG_BROADCAST_MESSAGE
//#define REQUIRE_LISTENER
using System;
using System.Collections.Generic;
using UnityEngine;

public enum Message
{
	/// <summary>
	/// This is the default message
	/// </summary>
	none = 0,
    CheckForSolution,
    OnStartFadeToTransparent,
    OnStartFadeToOpaque,
    OnEndFadeToTransparent,
    OnEndFadeToOpaque,
    StartNewGame,
    GameWon,
    GameLost,
    OnIncrementProgress,
    OnResetProgress,
    OnSetTimer,
    OnSetTimerMax,
    SaveGame,
	SetHighscoreAndCurrentScore,
	SaveGameCounter,

    // Arcade 1v1 messages
    ArcadeRoundWon,              // local player solved correctly in arcade mode
    ArcadeMatchStarted,          // match begins
    ArcadeRoundEnded,            // round result determined
    ArcadeMatchEnded,            // match finished
    ArcadeOpponentDisconnected,  // opponent lost connection

    // Segment or background colour changed in Settings. Anything that has to
    // stay readable against the board listens for this.
    OnThemeChanged,
}

public enum ReceiveMessage
{	
	/// <summary>
	/// This is the default receive message
	/// </summary>
	none = 0,
    ReceiveActiveNumberColor,
    ReceivePossibleNumberColor,
    ReceiveImpossibleNumberColor,
    ReceiveActiveInnerColor,
    ReceivePossibleImpossibleInnerColor,
	ReceiveGameProgress,
	ReceiveWinGUIColor,
	ReceiveLoseGUIColor,

}

static internal class Messenger
{
	#region Internal variables
	
	//Disable the unused variable warning
	#pragma warning disable 0414
	//Ensures that the MessengerHelper will be created automatically upon start of the game.
	//static private MessengerHelper messengerHelper = (new GameObject ("MessengerHelper")).AddComponent< MessengerHelper > ();
	#pragma warning restore 0414
	
	static public Dictionary<Message, Delegate> eventTable = new Dictionary<Message, Delegate> ();
	static public Dictionary<ReceiveMessage, Delegate> eventReceiverTable = new Dictionary<ReceiveMessage, Delegate> ();
	
	//Message handlers that should never be removed, regardless of calling Cleanup
	static public List< Message > permanentMessages = new List< Message > ();

	#endregion

	#region Helper methods
	//Marks a certain message as permanent.
	static public void MarkAsPermanent (Message eventType)
	{
		#if LOG_ALL_MESSAGES
		Debug.Log("Messenger MarkAsPermanent \t\"" + eventType + "\"");
		#endif
		
		permanentMessages.Add (eventType);
	}
	
	static public void Cleanup ()
	{
		#if LOG_ALL_MESSAGES
		Debug.Log("MESSENGER Cleanup. Make sure that none of necessary listeners are removed.");
		#endif
		
		List< Message > messagesToRemove = new List<Message> ();
		
		foreach (KeyValuePair<Message, Delegate> pair in eventTable) {
			bool wasFound = false;
			
			foreach (Message message in permanentMessages) {
				if (pair.Key == message) {
					wasFound = true;
					break;
				}
			}
			
			if (!wasFound)
				messagesToRemove.Add (pair.Key);
		}
		
		foreach (Message message in messagesToRemove) {
			eventTable.Remove (message);
		}

		eventReceiverTable.Clear();
	}
	
	static public void PrintEventTable ()
	{
		Debug.Log ("\t\t\t=== MESSENGER PrintEventTable ===");
		
		foreach (KeyValuePair<Message, Delegate> pair in eventTable) {
			Debug.Log ("\t\t\t" + pair.Key + "\t\t" + pair.Value);
		}
		
		Debug.Log ("\n");
	}

	static public void PrintEventReceiverTable()
	{
		Debug.Log ("\t\t\t=== RECEIVER PrintEventReceiverTable ===");
		
		foreach (KeyValuePair<ReceiveMessage, Delegate> pair in eventReceiverTable) {
			Debug.Log ("\t\t\t" + pair.Key + "\t\t" + pair.Value);
		}
		
		Debug.Log ("\n");
	}
	#endregion
	
	#region Message logging and exception throwing
	static public void OnListenerAdding (Message eventType, Delegate listenerBeingAdded)
	{
		#if LOG_ALL_MESSAGES || LOG_ADD_LISTENER
		Debug.Log("MESSENGER OnListenerAdding \t\"" + eventType + "\"\t{" + listenerBeingAdded.Target + " -> " + listenerBeingAdded.Method + "}");
		#endif
		
		if (!eventTable.ContainsKey (eventType)) {
			eventTable.Add (eventType, null);
		}
		
		Delegate d = eventTable [eventType];
		if (d != null && d.GetType () != listenerBeingAdded.GetType ()) {
			throw new ListenerException (string.Format ("Attempting to add listener with inconsistent signature for event type {0}. Current listeners have type {1} and listener being added has type {2}", 
			                                            eventType, 
			                                            d.GetType ().Name, 
			                                            listenerBeingAdded.GetType ().Name));
		}
	}
	
	static public void OnListenerRemoving (Message eventType, Delegate listenerBeingRemoved)
	{
		#if LOG_ALL_MESSAGES
		Debug.Log("MESSENGER OnListenerRemoving \t\"" + eventType + "\"\t{" + listenerBeingRemoved.Target + " -> " + listenerBeingRemoved.Method + "}");
		#endif
		
		if (eventTable.ContainsKey (eventType)) {
			Delegate d = eventTable [eventType];
			
			if (d == null) {
				throw new ListenerException (string.Format ("Attempting to remove listener with for event type \"{0}\" but current listener is null.", eventType));
			}  else if (d.GetType () != listenerBeingRemoved.GetType ()) {
				throw new ListenerException (string.Format ("Attempting to remove listener with inconsistent signature for event type {0}. Current listeners have type {1} and listener being removed has type {2}", 
				                                            eventType, d.GetType ().Name, 
				                                            listenerBeingRemoved.GetType ().Name));
			}
		}  else {
			throw new ListenerException (string.Format ("Attempting to remove listener for type \"{0}\" but Messenger doesn't know about this event type.", eventType));
		}
	}
	
	static public void OnListenerRemoved (Message eventType)
	{
		if (eventTable [eventType] == null) {
			eventTable.Remove (eventType);
		}
	}
	
	static public void OnBroadcasting (Message eventType)
	{
		#if REQUIRE_LISTENER
		if (!eventTable.ContainsKey (eventType)) {
			throw new BroadcastException (string.Format ("Broadcasting message \"{0}\" but no listener found. Try marking the message with Messenger.MarkAsPermanent.", eventType));
		}
		#endif
	}
	
	static public BroadcastException CreateBroadcastSignatureException (Message eventType)
	{
		return new BroadcastException (string.Format ("Broadcasting message \"{0}\" but listeners have a different signature than the broadcaster.", eventType));
	}
	
	public class BroadcastException : Exception
	{
		public BroadcastException (string msg)
			: base(msg)
		{
		}
	}
	
	public class ListenerException : Exception
	{
		public ListenerException (string msg)
			: base(msg)
		{
		}
	}
	#endregion
	
	#region AddListener
	//No parameters
	static public void AddListener (Message eventType, Callback handler)
	{
		OnListenerAdding (eventType, handler);
		eventTable [eventType] = (Callback)eventTable [eventType] + handler;
	}
	
	//Single parameter
	static public void AddListener<T> (Message eventType, Callback<T> handler)
	{
		OnListenerAdding (eventType, handler);
		eventTable [eventType] = (Callback<T>)eventTable [eventType] + handler;
	}
	
	//Two parameters
	static public void AddListener<T, U> (Message eventType, Callback<T, U> handler)
	{
		OnListenerAdding (eventType, handler);
		eventTable [eventType] = (Callback<T, U>)eventTable [eventType] + handler;
	}
	
	//Three parameters
	static public void AddListener<T, U, V> (Message eventType, Callback<T, U, V> handler)
	{
		OnListenerAdding (eventType, handler);
		eventTable [eventType] = (Callback<T, U, V>)eventTable [eventType] + handler;
	}
	#endregion
	
	#region RemoveListener
	/// <summary>
	/// Removes a listener only if the table still knows this event. Teardown
	/// order is not guaranteed, and MessengerCleaner wipes the whole table in
	/// its own OnDisable — a component unregistering after that would hit the
	/// exception RemoveListener throws for an unknown event.
	/// </summary>
	static public void TryRemoveListener (Message eventType, Callback handler)
	{
		Delegate d;
		if (eventTable.TryGetValue (eventType, out d) && d != null)
			RemoveListener (eventType, handler);
	}

	//No parameters
	static public void RemoveListener (Message eventType, Callback handler)
	{
		OnListenerRemoving (eventType, handler);   
		eventTable [eventType] = (Callback)eventTable [eventType] - handler;
		OnListenerRemoved (eventType);
	}
	
	//Single parameter
	static public void RemoveListener<T> (Message eventType, Callback<T> handler)
	{
		OnListenerRemoving (eventType, handler);
		eventTable [eventType] = (Callback<T>)eventTable [eventType] - handler;
		OnListenerRemoved (eventType);
	}
	
	//Two parameters
	static public void RemoveListener<T, U> (Message eventType, Callback<T, U> handler)
	{
		OnListenerRemoving (eventType, handler);
		eventTable [eventType] = (Callback<T, U>)eventTable [eventType] - handler;
		OnListenerRemoved (eventType);
	}
	
	//Three parameters
	static public void RemoveListener<T, U, V> (Message eventType, Callback<T, U, V> handler)
	{
		OnListenerRemoving (eventType, handler);
		eventTable [eventType] = (Callback<T, U, V>)eventTable [eventType] - handler;
		OnListenerRemoved (eventType);
	}
	#endregion
	
	#region Broadcast
	//No parameters
	static public void Broadcast (Message eventType)
	{
		#if LOG_ALL_MESSAGES || LOG_BROADCAST_MESSAGE
		Debug.Log("MESSENGER\t" + System.DateTime.Now.ToString("hh:mm:ss.fff") + "\t\t\tInvoking \t\"" + eventType + "\"");
		#endif
		OnBroadcasting (eventType);
		
		Delegate d;
		if (eventTable.TryGetValue (eventType, out d)) {
			Callback callback = d as Callback;
			
			if (callback != null) {
				callback ();
			}  else {
				throw CreateBroadcastSignatureException (eventType);
			}
		}
	}
	
	//Single parameter
	static public void Broadcast<T> (Message eventType, T arg1)
	{
		#if LOG_ALL_MESSAGES || LOG_BROADCAST_MESSAGE
		Debug.Log("MESSENGER\t" + System.DateTime.Now.ToString("hh:mm:ss.fff") + "\t\t\tInvoking \t\"" + eventType + "\"");
		#endif
		OnBroadcasting (eventType);
		
		Delegate d;
		if (eventTable.TryGetValue (eventType, out d)) {
			Callback<T> callback = d as Callback<T>;
			
			if (callback != null) {
				callback (arg1);
			}  else {
				throw CreateBroadcastSignatureException (eventType);
			}
		}
	}
	
	//Two parameters
	static public void Broadcast<T, U> (Message eventType, T arg1, U arg2)
	{
		#if LOG_ALL_MESSAGES || LOG_BROADCAST_MESSAGE
		Debug.Log("MESSENGER\t" + System.DateTime.Now.ToString("hh:mm:ss.fff") + "\t\t\tInvoking \t\"" + eventType + "\"");
		#endif
		OnBroadcasting (eventType);
		
		Delegate d;
		if (eventTable.TryGetValue (eventType, out d)) {
			Callback<T, U> callback = d as Callback<T, U>;
			
			if (callback != null) {
				callback (arg1, arg2);
			}  else {
				throw CreateBroadcastSignatureException (eventType);
			}
		}
	}
	
	//Three parameters
	static public void Broadcast<T, U, V> (Message eventType, T arg1, U arg2, V arg3)
	{
		#if LOG_ALL_MESSAGES || LOG_BROADCAST_MESSAGE
		Debug.Log("MESSENGER\t" + System.DateTime.Now.ToString("hh:mm:ss.fff") + "\t\t\tInvoking \t\"" + eventType + "\"");
		#endif
		OnBroadcasting (eventType);
		
		Delegate d;
		if (eventTable.TryGetValue (eventType, out d)) {
			Callback<T, U, V> callback = d as Callback<T, U, V>;
			
			if (callback != null) {
				callback (arg1, arg2, arg3);
			}  else {
				throw CreateBroadcastSignatureException (eventType);
			}
		}
	}
	#endregion

	#region Receive message logging and exception throwing
	static public void OnReceiverListenerAdding (ReceiveMessage eventType, Delegate listenerBeingAdded)
	{
		#if LOG_ALL_MESSAGES || LOG_ADD_LISTENER
		Debug.Log("MESSENGER OnListenerAdding \t\"" + eventType + "\"\t{" + listenerBeingAdded.Target + " -> " + listenerBeingAdded.Method + "}");
		#endif
		
		if (!eventReceiverTable.ContainsKey (eventType)) {
			eventReceiverTable.Add (eventType, null);
		}
		
		Delegate d = eventReceiverTable [eventType];
		if (d != null && d.GetType () != listenerBeingAdded.GetType ()) {
			throw new ListenerException (string.Format ("Attempting to add listener with inconsistent signature for event type {0}. Current listeners have type {1} and listener being added has type {2}", 
			                                            eventType, 
			                                            d.GetType ().Name, 
			                                            listenerBeingAdded.GetType ().Name));
		}
	}
	
	static public void OnReceiverListenerRemoving (ReceiveMessage eventType, Delegate listenerBeingRemoved)
	{
		#if LOG_ALL_MESSAGES
		Debug.Log("MESSENGER OnListenerRemoving \t\"" + eventType + "\"\t{" + listenerBeingRemoved.Target + " -> " + listenerBeingRemoved.Method + "}");
		#endif
		
		if (eventReceiverTable.ContainsKey (eventType)) {
			Delegate d = eventReceiverTable [eventType];
			
			if (d == null) {
				throw new ListenerException (string.Format ("Attempting to remove listener with for event type \"{0}\" but current listener is null.", eventType));
			}  else if (d.GetType () != listenerBeingRemoved.GetType ()) {
				throw new ListenerException (string.Format ("Attempting to remove listener with inconsistent signature for event type {0}. Current listeners have type {1} and listener being removed has type {2}", 
				                                            eventType, d.GetType ().Name, 
				                                            listenerBeingRemoved.GetType ().Name));
			}
		}  else {
			throw new ListenerException (string.Format ("Attempting to remove listener for type \"{0}\" but Messenger doesn't know about this event type.", eventType));
		}
	}
	
	static public void OnReceiverListenerRemoved (ReceiveMessage eventType)
	{
		if (eventReceiverTable [eventType] == null) {
			eventReceiverTable.Remove (eventType);
		}
	}
	
	static public void OnBroadcastingReceiver (ReceiveMessage eventType)
	{
		#if REQUIRE_LISTENER
		if (!eventReceiverTable.ContainsKey (eventType)) {
			throw new BroadcastException (string.Format ("Broadcasting message \"{0}\" but no listener found. Try marking the message with Messenger.MarkAsPermanent.", eventType));
		}
		#endif
	}
	
	static public BroadcastException CreateReceiverBroadcastSignatureException (ReceiveMessage eventType)
	{
		return new BroadcastException (string.Format ("Broadcasting message \"{0}\" but listeners have a different signature than the broadcaster.", eventType));
	}

	#endregion

	
	#region AddReceiverListener
	//No parameters
	static public void AddReceiverListener<R> (ReceiveMessage eventType, CallbackReceiver<R> handler)
	{
		OnReceiverListenerAdding (eventType, handler);
		eventReceiverTable [eventType] = (CallbackReceiver<R>)eventReceiverTable [eventType] + handler;
	}
	
	//Single parameter
	static public void AddReceiverListener<R, T> (ReceiveMessage eventType, CallbackReceiver<R, T> handler)
	{
		OnReceiverListenerAdding (eventType, handler);
		eventReceiverTable [eventType] = (CallbackReceiver<R, T>)eventReceiverTable [eventType] + handler;
	}
	
	//Two parameters
	static public void AddReceiverListener<R, T, U> (ReceiveMessage eventType, CallbackReceiver<R, T, U> handler)
	{
		OnReceiverListenerAdding (eventType, handler);
		eventReceiverTable [eventType] = (CallbackReceiver<R, T, U>)eventReceiverTable [eventType] + handler;
	}
	
	//Three parameters
	static public void AddReceiverListener<R, T, U, V> (ReceiveMessage eventType, CallbackReceiver<R, T, U, V> handler)
	{
		OnReceiverListenerAdding (eventType, handler);
		eventReceiverTable [eventType] = (CallbackReceiver<R, T, U, V>)eventReceiverTable [eventType] + handler;
	}
	#endregion
	
	#region RemoveReceiverListener
	//No parameters
	static public void RemoveReceiverListener<R> (ReceiveMessage eventType, CallbackReceiver<R> handler)
	{
		OnReceiverListenerRemoving (eventType, handler);   
		eventReceiverTable [eventType] = (CallbackReceiver<R>)eventReceiverTable [eventType] - handler;
		OnReceiverListenerRemoved (eventType);
	}
	
	//Single parameter
	static public void RemoveReceiverListener<R, T> (ReceiveMessage eventType, CallbackReceiver<R, T> handler)
	{
		OnReceiverListenerRemoving (eventType, handler);
		eventReceiverTable [eventType] = (CallbackReceiver<R, T>)eventReceiverTable [eventType] - handler;
		OnReceiverListenerRemoved (eventType);
	}
	
	//Two parameters
	static public void RemoveReceiverListener<R, T, U> (ReceiveMessage eventType, CallbackReceiver<R, T, U> handler)
	{
		OnReceiverListenerRemoving (eventType, handler);
		eventReceiverTable [eventType] = (CallbackReceiver<R, T, U>)eventReceiverTable [eventType] - handler;
		OnReceiverListenerRemoved (eventType);
	}
	
	//Three parameters
	static public void RemoveReceiverListener<R, T, U, V> (ReceiveMessage eventType, CallbackReceiver<R, T, U, V> handler)
	{
		OnReceiverListenerRemoving (eventType, handler);
		eventReceiverTable [eventType] = (CallbackReceiver<R, T, U, V>)eventReceiverTable [eventType] - handler;
		OnReceiverListenerRemoved (eventType);
	}
	#endregion
	
	#region Broadcast
	//No parameters
	static public R BroadcastReceiver<R> (ReceiveMessage eventType)
	{
		#if LOG_ALL_MESSAGES || LOG_BROADCAST_MESSAGE
		Debug.Log("MESSENGER\t" + System.DateTime.Now.ToString("hh:mm:ss.fff") + "\t\t\tInvoking \t\"" + eventType + "\"");
		#endif
		OnBroadcastingReceiver (eventType);
		
		Delegate d;
		if (eventReceiverTable.TryGetValue (eventType, out d)) {
			CallbackReceiver<R> callback = d as CallbackReceiver<R>;
			
			if (callback != null) {
				return callback ();
			}  else {
				throw CreateReceiverBroadcastSignatureException (eventType);
			}
		}

		Debug.LogWarning(eventType.ToString() + " listener not created");

		return default(R);
	}
	
	//Single parameter
	static public R BroadcastReceiver<R, T> (ReceiveMessage eventType, T arg1)
	{
		#if LOG_ALL_MESSAGES || LOG_BROADCAST_MESSAGE
		Debug.Log("MESSENGER\t" + System.DateTime.Now.ToString("hh:mm:ss.fff") + "\t\t\tInvoking \t\"" + eventType + "\"");
		#endif
		OnBroadcastingReceiver (eventType);
		
		Delegate d;
		if (eventReceiverTable.TryGetValue (eventType, out d)) {
			CallbackReceiver<R, T> callback = d as CallbackReceiver<R, T>;
			
			if (callback != null) {
				return callback (arg1);
			}  else {
				throw CreateReceiverBroadcastSignatureException (eventType);
			}
		}
		
		Debug.LogWarning(eventType.ToString() + " listener not created");
		
		return default(R);
	}
	
	//Two parameters
	static public R BroadcastReceiver<R, T, U> (ReceiveMessage eventType, T arg1, U arg2)
	{
		#if LOG_ALL_MESSAGES || LOG_BROADCAST_MESSAGE
		Debug.Log("MESSENGER\t" + System.DateTime.Now.ToString("hh:mm:ss.fff") + "\t\t\tInvoking \t\"" + eventType + "\"");
		#endif
		OnBroadcastingReceiver (eventType);
		
		Delegate d;
		if (eventReceiverTable.TryGetValue (eventType, out d)) {
			CallbackReceiver<R, T, U> callback = d as CallbackReceiver<R, T, U>;
			
			if (callback != null) {
				return callback (arg1, arg2);
			}  else {
				throw CreateReceiverBroadcastSignatureException (eventType);
			}
		}
		
		Debug.LogWarning(eventType.ToString() + " listener not created");
		
		return default(R);
	}
	
	//Three parameters
	static public R BroadcastReceiver<R, T, U, V> (ReceiveMessage eventType, T arg1, U arg2, V arg3)
	{
		#if LOG_ALL_MESSAGES || LOG_BROADCAST_MESSAGE
		Debug.Log("MESSENGER\t" + System.DateTime.Now.ToString("hh:mm:ss.fff") + "\t\t\tInvoking \t\"" + eventType + "\"");
		#endif
		OnBroadcastingReceiver (eventType);
		
		Delegate d;
		if (eventReceiverTable.TryGetValue (eventType, out d)) {
			CallbackReceiver<R, T, U, V> callback = d as CallbackReceiver<R, T, U, V>;
			
			if (callback != null) {
				return callback (arg1, arg2, arg3);
			}  else {
				throw CreateReceiverBroadcastSignatureException (eventType);
			}
		}
		
		Debug.LogWarning(eventType.ToString() + " listener not created");
		
		return default(R);
	}
	#endregion
}



