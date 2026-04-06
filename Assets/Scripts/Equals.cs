using UnityEngine;
using System.Collections;

public class Equals : MonoBehaviour 
{
	public Line line1 = null;
	public Line line2 = null;

	void Start()
	{
		line1.SetSelected ();
		line2.SetSelected ();
	}

}
