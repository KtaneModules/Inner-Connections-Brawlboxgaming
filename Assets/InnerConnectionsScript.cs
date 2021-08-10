using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;
using System.Text.RegularExpressions;

public class InnerConnectionsScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMRuleSeedable RuleSeedable;
    public KMSelectable StartButton, LeftArrow, RightArrow;
    public GameObject LeftDoor, RightDoor, RightDoorFrame, WireParent;
    public MeshRenderer LED;
    public Material[] WireMats, LEDColoursOn, LEDColoursOff;
    public GameObject[] Wires;
    public KMSelectable[] WireSelectables;
    public TextMesh TimerText;

    private Coroutine timer;
    private static int _moduleIdCounter = 1;
    private int _moduleId, rndLEDColour, morseNumber, wiresPosition = 0, ix;
    private bool _moduleSolved, _doorOpen, wiresMoving = false;
    private static readonly string[] morseArray = new[] { "-----", ".----", "..---", "...--", "....-", ".....", "-....", "--...", "---..", "----." },
                                     colourList = new[] { "Black", "Blue", "Red", "White", "Yellow" };
    private List<int[]> firstColourTable = new List<int[]>();
    private int[] rndArray = new[] { 0, 1, 2, 3, 4 }, rndColoursArray = new[] { 0, 1, 2, 3, 4 }, exceptionWires = new int[2], secondWires = new int[5], wireColours = new int[18];
    private float[] childrenPosX;
    private float parentPosX;
    private int firstWireColour, secondWireColour;
    private bool[] wiresCut = new[] { false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false },
           wiresNeededToCut = new[] { false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false };

    void Start()
    {
        var random = RuleSeedable.GetRNG();
        random.ShuffleFisherYates(rndColoursArray);
        for (int i = 0; i < 10; i++)
        {
            random.ShuffleFisherYates(rndColoursArray);
            firstColourTable.Add(rndColoursArray.ToArray());
        }

        random.ShuffleFisherYates(rndColoursArray);

        for (int i = 0; i < 5; i++)
        {
            secondWires[i] = rndColoursArray[i];
        }

        random.ShuffleFisherYates(rndColoursArray);

        exceptionWires[0] = rndColoursArray[0];
        exceptionWires[1] = rndColoursArray[1];

        childrenPosX = Wires.Select(w => w.transform.localPosition.x).ToArray();
        parentPosX = WireParent.transform.localPosition.x;

        WireDisabling();

        _moduleId = _moduleIdCounter++;
        StartButton.OnInteract = StartButtonHandler;
        LeftArrow.OnInteract = LeftArrowButtonHandler;
        RightArrow.OnInteract = RightArrowButtonHandler;
        for (int i = 0; i < WireSelectables.Length; i++)
        {
            int j = i;
            WireSelectables[i].OnInteract += delegate ()
              {
                  WireHandler(j);
                  return false;
              };
        }

        morseNumber = Rnd.Range(0, 10);
        rndLEDColour = Rnd.Range(0, 5);

        Debug.LogFormat("[Inner Connections #{0}] The rule seed is {1}.", _moduleId, random.Seed);
        Debug.LogFormat("[Inner Connections #{0}] The LED's displayed number is {1}.", _moduleId, morseNumber);
        Debug.LogFormat("[Inner Connections #{0}] The LED's colour is {1}.", _moduleId, colourList[rndLEDColour]);

        for (int i = 0; i < Wires.Length / 2; i++)
        {
            if (i % 5 == 0)
            {
                rndArray.Shuffle();
            }
            var rndWireColour = rndArray[i % 5];
            Wires[i].GetComponent<MeshRenderer>().material = WireMats[rndWireColour];
            Wires[i + 18].GetComponent<MeshRenderer>().material = WireMats[rndWireColour];
            wireColours[i] = rndWireColour;
        }

        if (morseNumber == BombInfo.GetIndicators().Count())
        {
            Debug.LogFormat("[Inner Connections #{0}] The LED's displayed number is equal to the number of indicators: {1}.", _moduleId, morseNumber);
            firstWireColour = rndLEDColour;
        }
        else
        {
            int calculatedNumber = ((BombInfo.GetBatteryCount() + morseNumber) * colourList[rndLEDColour].Count()) % 9;

            Debug.LogFormat("[Inner Connections #{0}] The calculated number is {1}.", _moduleId, calculatedNumber);

            var ports = BombInfo.GetPorts().ToArray();
            if (ports.Contains("DVI"))
                ix = 0;
            else if (ports.Contains("Parallel"))
                ix = 1;
            else if (ports.Contains("PS2"))
                ix = 2;
            else if (ports.Contains("RJ45"))
                ix = 3;
            else if (ports.Contains("Serial"))
                ix = 4;
            else if (ports.Contains("StereoRCA"))
            {
                ix = -1;
                firstWireColour = exceptionWires[0];
            }
            else
            {
                ix = -1;
                firstWireColour = exceptionWires[1];
            }
            if (ix > -1)
            {
                firstWireColour = firstColourTable[calculatedNumber][ix];
            }
        }
        Debug.LogFormat("[Inner Connections #{0}] The first wire colour to cut is {1}.", _moduleId, colourList[firstWireColour]);

        StartCoroutine(LEDFlash());
    }

    private void WireDisabling()
    {
        if (_doorOpen)
        {
            for (int i = 0; i < 18; i++)
            {
                int j = i;
                if (wiresCut[i])
                {
                    Wires[i].SetActive(false);
                    j = i + 18;
                }

                if (childrenPosX[j] + parentPosX < -0.0475f || childrenPosX[j] + parentPosX > 0.0475f)
                    Wires[j].SetActive(false);

                else
                    Wires[j].SetActive(true);
            }
        }
        else
        {
            for (int i = 0; i < childrenPosX.Length; i++)
            {
                Wires[i].SetActive(false);
            }
        }
    }

    private IEnumerator Timer()
    {
        for (int i = 15; i > 0; i--)
        {
            TimerText.text = i.ToString("00");
            yield return new WaitForSeconds(1f);
        }
        TimerText.text = "--";
        if (!_moduleSolved)
        {
            Debug.LogFormat("[Inner Connections #{0}] You ran out of time. Strike!", _moduleId);
            Strike();
            yield return new WaitForSeconds(1f);
            WireDisabling();
        }
    }

    private void Pass()
    {
        Debug.LogFormat("[Inner Connections #{0}] All required wires have been cut. Module solved!", _moduleId);
        Module.HandlePass();
        StartCoroutine(Move(LeftDoor.transform, -0.03f, 0f, -0.04035617f, 0f));
        StartCoroutine(Move(RightDoor.transform, 0.04f, 0f, -0.04035617f, 0f));
        StartCoroutine(Move(RightDoorFrame.transform, 0.04f, 0f, -0.04035617f, 0f));
        StopCoroutine(timer);
        TimerText.text = "--";
        _doorOpen = false;
        _moduleSolved = true;
    }

    private void Strike()
    {
        if (!_moduleSolved)
            Module.HandleStrike();
        StartCoroutine(Move(LeftDoor.transform, -0.03f, 0f, -0.04035617f, 0f));
        StartCoroutine(Move(RightDoor.transform, 0.04f, 0f, -0.04035617f, 0f));
        StartCoroutine(Move(RightDoorFrame.transform, 0.04f, 0f, -0.04035617f, 0f));
        StopCoroutine(timer);
        TimerText.text = "--";
        _doorOpen = false;
    }

    private IEnumerator LEDFlash()
    {
        while (!_moduleSolved)
        {
            var morse = morseArray[morseNumber];
            for (int c = 0; c < morse.Length; c++)
            {
                LED.sharedMaterial = LEDColoursOn[rndLEDColour];
                yield return new WaitForSeconds(morse[c] == '.' ? 0.25f : 0.75f);
                LED.sharedMaterial = LEDColoursOff[rndLEDColour];
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(1.5f);
        }
    }

    private bool StartButtonHandler()
    {
        if (_moduleSolved)
            return false;
        if (!_doorOpen)
        {
            _doorOpen = true;
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, StartButton.transform);
            StartButton.AddInteractionPunch();
            StartCoroutine(Move(LeftDoor.transform, 0f, -0.03f, -0.04035617f, 0f));
            StartCoroutine(Move(RightDoor.transform, 0f, 0.04f, -0.04035617f, 0f));
            StartCoroutine(Move(RightDoorFrame.transform, 0f, 0.04f, -0.04035617f, 0f));
            timer = StartCoroutine(Timer());
        }

        double solvedModules = BombInfo.GetSolvedModuleNames().Count;
        double unsolvedModules = BombInfo.GetSolvableModuleNames().Count - BombInfo.GetSolvedModuleNames().Count;

        double ratio = solvedModules / unsolvedModules;
        string lessThanRatio = "";

        if (ratio < 1.0 / 4.0)
        {
            secondWireColour = secondWires[0];
            lessThanRatio = "less than 1:4";
        }

        else if (ratio < 1.0 / 2.0)
        {
            secondWireColour = secondWires[1];
            lessThanRatio = "less than 1:2";
        }

        else if (ratio < 1.0)
        {
            secondWireColour = secondWires[2];
            lessThanRatio = "less than 1:1";
        }

        else if (ratio < 2.0)
        {
            secondWireColour = secondWires[3];
            lessThanRatio = "less than 2:1";
        }

        else
        {
            secondWireColour = secondWires[4];
            lessThanRatio = "greater than or equal to 2:1";
        }
        ix = 0;
        string duplicate = "";
    tryagain:
        if (firstWireColour == secondWireColour)
        {
            secondWireColour = firstColourTable[0][ix];
            duplicate = "There is a duplicate colour. ";
            ix++;
            goto tryagain;
        }
        Debug.LogFormat("[Inner Connections #{0}] The ratio of solved:unsolved modules is {1}. {2}The second wire colour to cut is {3}.", _moduleId, lessThanRatio, duplicate, colourList[secondWireColour]);

        for (int i = 0; i < Wires.Length / 2; i++)
        {
            if (wireColours[i] == firstWireColour || wireColours[i] == secondWireColour)
                wiresNeededToCut[i] = true;
        }

        return false;
    }

    private void WireHandler(int num)
    {
        if (_doorOpen)
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.WireSnip, Wires[num].transform);
            wiresCut[num] = true;
            WireDisabling();
            bool correctCuts = true;
            if (!wiresNeededToCut[num])
            {
                Debug.LogFormat("[Inner Connections #{0}] You cut a {1} wire. Strike!", _moduleId, colourList[wireColours[num]]);
                Strike();
            }
            for (int i = 0; i < wiresNeededToCut.Length; i++)
            {
                if (wiresNeededToCut[i] && !wiresCut[i])
                {
                    correctCuts = false;
                }
            }
            if (correctCuts)
                Pass();
        }
    }

    private bool LeftArrowButtonHandler()
    {
        if (_moduleSolved)
            return false;
        if (wiresMoving)
            return false;
        if (_doorOpen)
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, LeftArrow.transform);
            if (wiresPosition == 2)
            {
                StartCoroutine(Move(WireParent.transform, -0.126f, -0.063f, 0.009f, 0f));
                wiresPosition = 1;
            }
            else if (wiresPosition == 1)
            {
                StartCoroutine(Move(WireParent.transform, -0.063f, 0f, 0.009f, 0f));
                wiresPosition = 0;
            }
        }
        return false;
    }

    private bool RightArrowButtonHandler()
    {
        if (_moduleSolved)
            return false;
        if (wiresMoving)
            return false;
        if (_doorOpen)
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, RightArrow.transform);
            if (wiresPosition == 0)
            {
                StartCoroutine(Move(WireParent.transform, 0f, -0.063f, 0.009f, 0f));
                wiresPosition = 1;
            }
            else if (wiresPosition == 1)
            {
                StartCoroutine(Move(WireParent.transform, -0.063f, -0.126f, 0.009f, 0f));
                wiresPosition = 2;
            }
        }
        return false;
    }

    private IEnumerator Move(Transform obj, float startPosX, float endPosX, float posY, float posX)
    {
        wiresMoving = true;
        var duration = 1f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            obj.localPosition = new Vector3(Easing.InOutQuad(elapsed, startPosX, endPosX, duration), posY, posX);
            childrenPosX = Wires.Select(w => w.transform.localPosition.x).ToArray();
            parentPosX = WireParent.transform.localPosition.x;
            yield return null;
            elapsed += Time.deltaTime;
            if (_doorOpen)
                WireDisabling();
        }
        obj.localPosition = new Vector3(endPosX, posY, posX);
        wiresMoving = false;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} submit <colour1> <colour2> [colour names are: Black/K, Blue/B, Red/R, White/W, Yellow/Y]";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        var match = Regex.Match(command.ToLowerInvariant(), @"^\s*submit\s+(.*?)\s+(.*?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (match.Success)
        {
            int colourInt1 = -1;
            int colourInt2 = -1;
            switch (match.Groups[1].Value.Trim())
            {
                case "k":
                case "black":
                    colourInt1 = 0;
                    break;
                case "b":
                case "blue":
                    colourInt1 = 1;
                    break;
                case "r":
                case "red":
                    colourInt1 = 2;
                    break;
                case "w":
                case "white":
                    colourInt1 = 3;
                    break;
                case "y":
                case "yellow":
                    colourInt1 = 4;
                    break;
            }
            switch (match.Groups[2].Value.Trim())
            {
                case "k":
                case "black":
                    colourInt2 = 0;
                    break;
                case "b":
                case "blue":
                    colourInt2 = 1;
                    break;
                case "r":
                case "red":
                    colourInt2 = 2;
                    break;
                case "w":
                case "white":
                    colourInt2 = 3;
                    break;
                case "y":
                case "yellow":
                    colourInt2 = 4;
                    break;
            }

            if (colourInt1 == -1 || colourInt2 == -1)
                yield break;

            yield return null;

            StartButton.OnInteract();
            yield return new WaitForSeconds(1f);

            while (wiresPosition > 0)
            {
                LeftArrow.OnInteract();
                yield return new WaitForSeconds(1f);
            }

            for (int i = 0; i < 6; i++)
            {
                if ((wireColours[i] == colourInt1 || wireColours[i] == colourInt2) && !wiresCut[i])
                {
                    WireSelectables[i].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
            }
            RightArrow.OnInteract();
            yield return new WaitForSeconds(1f);
            for (int i = 6; i < 12; i++)
            {
                if ((wireColours[i] == colourInt1 || wireColours[i] == colourInt2) && !wiresCut[i])
                {
                    WireSelectables[i].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
            }
            RightArrow.OnInteract();
            yield return new WaitForSeconds(1f);
            for (int i = 12; i < 18; i++)
            {
                if ((wireColours[i] == colourInt1 || wireColours[i] == colourInt2) && !wiresCut[i])
                {
                    WireSelectables[i].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
            }
            yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
        StartButton.OnInteract();
        yield return new WaitForSeconds(1f);

        while (wiresPosition > 0)
        {
            LeftArrow.OnInteract();
            yield return new WaitForSeconds(1f);
        }

        for (int i = 0; i < 6; i++)
        {
            if (wiresNeededToCut[i] && !wiresCut[i])
            {
                WireSelectables[i].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
        }
        RightArrow.OnInteract();
        yield return new WaitForSeconds(1f);
        for (int i = 6; i < 12; i++)
        {
            if (wiresNeededToCut[i] && !wiresCut[i])
            {
                WireSelectables[i].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
        }
        RightArrow.OnInteract();
        yield return new WaitForSeconds(1f);
        for (int i = 12; i < 18; i++)
        {
            if (wiresNeededToCut[i] && !wiresCut[i])
            {
                WireSelectables[i].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
        }
        yield break;
    }
}
