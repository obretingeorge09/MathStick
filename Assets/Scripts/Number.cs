using UnityEngine;
using System.Collections;

public class Number : MonoBehaviour
{
    public Digit FirstDigit = null;   // tens (2-digit) or tens (3-digit)
    public Digit SecondDigit = null;  // ones
    public Digit ThirdDigit = null;   // hundreds (optional, for 3-digit mode)

    public bool Is3Digit => ThirdDigit != null;

    public void Initialize(LinePositions lp1, LinePositions lp2)
    {
        FirstDigit.Initialize(lp1);
        SecondDigit.Initialize(lp2);
    }

    public void Initialize(LinePositions lpH, LinePositions lpT, LinePositions lpO)
    {
        if (ThirdDigit != null) ThirdDigit.Initialize(lpH);
        FirstDigit.Initialize(lpT);
        SecondDigit.Initialize(lpO);
    }

    public int GetNumber()
    {
        int d1 = FirstDigit.GetDigit();
        int d2 = SecondDigit.GetDigit();
        if (d1 == -1 || d2 == -1) return -1;

        if (ThirdDigit != null)
        {
            int d0 = ThirdDigit.GetDigit();
            if (d0 == -1) return -1;
            return d0 * 100 + d1 * 10 + d2;
        }

        return d1 * 10 + d2;
    }
}
