using UnityEngine;
using System.Collections;

public enum LinePositions
{
    Top = 0,
    Middle = 1,
    Bottom = 2,
    TopLeft = 3,
    TopRight = 4,
    BottomLeft = 5,
    BottomRight = 6,
    EnumLength = 7,
}

public class Digit : MonoBehaviour
{
    public Line[] Lines = null;

    public void Initialize(LinePositions lp)
    {
        for (int i = 0; i < Lines.Length; i++)
        {
            if (Lines[i] != null)
                Lines[i].SetActive();
        }
        // A slot can now be fully free: GameManager.NO_DEAD_SEGMENT is one past
        // the last real segment, and it is the only way digit 8 can be built.
        int idx = (int)lp;
        if (idx >= 0 && idx < Lines.Length && Lines[idx] != null)
            Lines[idx].SetInactive();
    }

    public int GetDigit()
    {
        bool[] areActive = new bool[(int)LinePositions.EnumLength];
        GetActiveLines(areActive);

        int sum = 0;
        for (int i = 0; i < areActive.Length; i++)
        {
            if (areActive[i]) sum++;
        }

        if (sum == 6 && !areActive[(int)LinePositions.Middle])
            return 0;
        else if (sum == 2 && areActive[(int)LinePositions.TopRight] && areActive[(int)LinePositions.BottomRight])
            return 1;
        else if (sum == 5 && !areActive[(int)LinePositions.TopLeft] && !areActive[(int)LinePositions.BottomRight])
            return 2;
        else if (sum == 5 && !areActive[(int)LinePositions.TopLeft] && !areActive[(int)LinePositions.BottomLeft])
            return 3;
        else if (sum == 4 && !areActive[(int)LinePositions.Top] && !areActive[(int)LinePositions.BottomLeft] && !areActive[(int)LinePositions.Bottom])
            return 4;
        else if (sum == 5 && !areActive[(int)LinePositions.TopRight] && !areActive[(int)LinePositions.BottomLeft])
            return 5;
        else if (sum == 6 && !areActive[(int)LinePositions.TopRight])
            return 6;
        else if (sum == 3 && areActive[(int)LinePositions.Top] && areActive[(int)LinePositions.TopRight] && areActive[(int)LinePositions.BottomRight])
            return 7;
        else if (sum == 7)
            return 8;
        else if (sum == 6 && !areActive[(int)LinePositions.BottomLeft])
            return 9;
        else
            return -1;
    }

    void GetActiveLines(bool[] activeLines)
    {
        if (activeLines.Length >= (int)LinePositions.EnumLength && Lines.Length >= (int)LinePositions.EnumLength)
        {
            for (int i = 0; i < (int)LinePositions.EnumLength; i++)
            {
                activeLines[i] = Lines[i].IsSelected();
            }
        }
    }
}
