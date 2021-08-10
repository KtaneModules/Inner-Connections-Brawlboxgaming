using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;

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
    private int _moduleId, rndLEDColour, rndWireColour, morseNumber, wiresPosition = 0, ix;
    private bool _moduleSolved, _doorOpen, wiresMoving = false;
    private static readonly string[] morseArray = new[] { "-----", ".----", "..---", "...--", "....-", ".....", "-....", "--...", "---..", "----." },
                                     colourList = new[] { "Black", "Blue", "Red", "White", "Yellow" };
    private string[][] firstColourTable = new[]{
        new[] { "Yellow", "Blue", "White", "Red", "Black" },
        new[] { "Black", "White", "Yellow", "Red", "Blue" },
        new[] { "Yellow", "Red", "Black", "Blue", "White" },
        new[] { "Red", "Yellow", "Blue", "Black", "White" },
        new[] { "Blue", "White", "Yellow", "Red", "Black" },
        new[] { "White", "Black", "Yellow", "Red", "Blue" },
        new[] { "Red", "Black", "Blue", "Yellow", "White" },
        new[] { "Yellow", "White", "Blue", "Black", "Red" },
        new[] { "Black", "Blue", "Yellow", "Red", "White" },
        new[] { "Blue", "Black", "Yellow", "Red", "White" } };
    private int[] rndArray = new[] { 0, 1, 2, 3, 4 }, rndColoursArray = new[] { 0, 1, 2, 3, 4 }, exceptionWires = new int[2], secondWires = new int[5];
    private float[] childrenPosX;
    private float parentPosX;
    private string firstWireColour = "", secondWireColour = "";
    private string[] wireColourNames = new string[18];
    private bool[] wiresCut = new[] { false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false },
           wiresNeededToCut = new[] { false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false };

    void Start()
    {
        var random = RuleSeedable.GetRNG();
        random.ShuffleFisherYates(rndColoursArray);
        for (int i = 0; i < 10; i++)
        {
            random.ShuffleFisherYates(rndColoursArray);
            for (int j = 0; j < 5; j++)
                firstColourTable[i][j] = colourList[rndColoursArray[j]];
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

        Debug.LogFormat("[Inner Connections #{0}] The LED's displayed number is {1}", _moduleId, morseNumber);
        Debug.LogFormat("[Inner Connections #{0}] The LED's colour is {1}", _moduleId, colourList[rndLEDColour]);

        for (int i = 0; i < Wires.Length / 2; i++)
        {
            if (i % 5 == 0)
            {
                rndArray.Shuffle();
            }
            rndWireColour = rndArray[i % 5];
            Wires[i].GetComponent<MeshRenderer>().material = WireMats[rndWireColour];
            Wires[i + 18].GetComponent<MeshRenderer>().material = WireMats[rndWireColour];
            wireColourNames[i] = colourList[rndWireColour];
        }

        if (morseNumber == BombInfo.GetIndicators().Count())
        {
            Debug.LogFormat("[Inner Connections #{0}] The LED's displayed number is equal to the number of indicators: {1}.", _moduleId, morseNumber);
            firstWireColour = colourList[rndLEDColour];
        }
        else
        {
            int calculatedNumber = ((BombInfo.GetBatteryCount() + morseNumber) * colourList[rndLEDColour].Count()) % 9;

            Debug.LogFormat("[Inner Connections #{0}] The calculated number is {1}", _moduleId, calculatedNumber);

            var ports = BombInfo.GetPorts().ToArray();
            if (ports.Contains("DVI"))
                ix = 0;
            else if (ports.Contains("Parallel"))
                ix = 1;
            else if (ports.Contains("PS/2"))
                ix = 2;
            else if (ports.Contains("RJ45"))
                ix = 3;
            else if (ports.Contains("Serial"))
                ix = 4;
            else if (ports.Contains("StereoRCA"))
            {
                ix = -1;
                firstWireColour = colourList[exceptionWires[0]];
            }
            else
            {
                ix = -1;
                firstWireColour = colourList[exceptionWires[1]];
            }
            if (ix > -1)
            {
                firstWireColour = firstColourTable[calculatedNumber][ix];
            }
        }
        Debug.LogFormat("[Inner Connections #{0}] The first wire colour to cut is {1}", _moduleId, firstWireColour);

        StartCoroutine(LEDFlash());
    }

    private void WireDisabling()
    {
        if (_doorOpen)
        {
            for (int i = 0; i < 18; i++)
            {
                if (wiresCut[i])
                {
                    Wires[i].SetActive(false);
                    i = i + 18;
                }

                if (childrenPosX[i] + parentPosX < -0.05f || childrenPosX[i] + parentPosX > 0.05f)
                    Wires[i].SetActive(false);

                else
                    Wires[i].SetActive(true);

                if (i > 17)
                    i = i - 18;
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
            TimerText.text = ((i+100)%100).ToString("00");
            yield return new WaitForSeconds(1f);
        }
        TimerText.text = "00";
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

        int solvedModules = BombInfo.GetSolvedModuleNames().Count();
        int unsolvedModules = BombInfo.GetSolvableModuleNames().Count() - BombInfo.GetSolvedModuleNames().Count();

        float ratio = solvedModules / unsolvedModules;
        string closestRatio = "";

        if (ratio >= 0 && ratio < 1.0 / 4.0)
        {
            secondWireColour = colourList[secondWires[0]];
            closestRatio = "0:1";
        }

        else if (ratio >= 1.0 / 4.0 && ratio < 3.0 / 4.0)
        {
            secondWireColour = colourList[secondWires[1]];
            closestRatio = "1:2";
        }

        else if (ratio >= 3.0 / 4.0 && ratio < 3.0 / 2.0)
        {
            secondWireColour = colourList[secondWires[2]];
            closestRatio = "1:1";
        }

        else if (ratio >= 3.0 / 2.0 && ratio < 5.0 / 2.0)
        {
            secondWireColour = colourList[secondWires[3]];
            closestRatio = "2:1";
        }

        else if (ratio >= 5.0 / 2.0)
        {
            secondWireColour = colourList[secondWires[4]];
            closestRatio = "3:1";
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
        Debug.LogFormat("[Inner Connections #{0}] The closest ratio of solved:unsolved modules is {1}. {2}The second wire colour to cut is {3}.", _moduleId, closestRatio, duplicate, secondWireColour);

        for (int i = 0; i < Wires.Length / 2; i++)
        {
            if (wireColourNames[i] == firstWireColour || wireColourNames[i] == secondWireColour)
                wiresNeededToCut[i] = true;
        }

        return false;
    }

    private void WireHandler(int num)
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.WireSnip, Wires[num].transform);
        wiresCut[num] = true;
        WireDisabling();
        bool correctCuts = true;
        if (!wiresNeededToCut[num])
        {
            Debug.LogFormat("[Inner Connections #{0}] You cut a {1} wire. Strike!", _moduleId, wireColourNames[num]);
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
                StartCoroutine(Move(WireParent.transform, 0f, -0.063f, 0.0065f, 0f));
                wiresPosition = 1;
            }
            else if (wiresPosition == 1)
            {
                StartCoroutine(Move(WireParent.transform, -0.063f, -0.126f, 0.0065f, 0f));
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
}
