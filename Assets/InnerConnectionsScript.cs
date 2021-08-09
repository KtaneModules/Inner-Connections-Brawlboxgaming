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

    private static int _moduleIdCounter = 1;
    private int _moduleId, rndLEDColour, rndWireColour, morseNumber, wiresPosition = 0;
    private bool _moduleSolved, _doorOpen, wiresMoving = false;
    private static readonly string[] morseArray = new[] { "-----", ".----", "..---", "...--", "....-", ".....", "-....", "--...", "---..", "----." },
                                     colourList = new[] { "K", "B", "R", "W", "Y" };
    private float[] childrenPosX;
    private float parentPosX;


    void Start()
    {
        childrenPosX = Wires.Select(w => w.transform.localPosition.x).ToArray();
        parentPosX = WireParent.transform.localPosition.x;

        WireDisabling();

        _moduleId = _moduleIdCounter++;
        StartButton.OnInteract = StartButtonHandler;
        LeftArrow.OnInteract = LeftArrowButtonHandler;
        RightArrow.OnInteract = RightArrowButtonHandler;

        morseNumber = Rnd.Range(0, 10);
        rndLEDColour = Rnd.Range(0, 5);

        for (int i = 0; i < Wires.Length; i++)
        {
            rndWireColour = Rnd.Range(0, 5);
            Wires[i].GetComponent<MeshRenderer>().material = WireMats[rndWireColour];
        }

        StartCoroutine(LEDFlash());
    }

    private void WireDisabling()
    {
        for (int i = 0; i < childrenPosX.Length; i++)
        {
            if (childrenPosX[i] + parentPosX < -0.075f || childrenPosX[i] + parentPosX > 0.073f)
                Wires[i].SetActive(false);

            else
                Wires[i].SetActive(true);
        }
    }

    private IEnumerator Timer()
    {
        yield return new WaitForSeconds(30f);
        if (!_moduleSolved)
        {
            Strike();
        }
    }

    private void Strike()
    {
        Module.HandleStrike();
        StartCoroutine(Move(LeftDoor.transform, -0.03f, 0f, -0.04035617f, 0f));
        StartCoroutine(Move(RightDoor.transform, 0.04f, 0f, -0.04035617f, 0f));
        StartCoroutine(Move(RightDoorFrame.transform, 0.04f, 0f, -0.04035617f, 0f));
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
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, StartButton.transform);
        StartButton.AddInteractionPunch();
        if (_moduleSolved)
            return false;
        if (!_doorOpen)
        {
            StartCoroutine(Move(LeftDoor.transform, 0f, -0.03f, -0.04035617f, 0f));
            StartCoroutine(Move(RightDoor.transform, 0f, 0.04f, -0.04035617f, 0f));
            StartCoroutine(Move(RightDoorFrame.transform, 0f, 0.04f, -0.04035617f, 0f));
            StartCoroutine(Timer());
            _doorOpen = true;
        }
        return false;
    }

    private bool LeftArrowButtonHandler()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, LeftArrow.transform);
        LeftArrow.AddInteractionPunch();
        if (_moduleSolved)
            return false;
        if (wiresMoving)
            return false;
        if (_doorOpen)
        {
            if (wiresPosition == 2)
            {
                StartCoroutine(Move(WireParent.transform, -0.126f, -0.063f, 0f, 0f));
                wiresPosition = 1;
            }
            else if (wiresPosition == 1)
            {
                StartCoroutine(Move(WireParent.transform, -0.063f, 0f, 0f, 0f));
                wiresPosition = 0;
            }
        }
        return false;
    }

    private bool RightArrowButtonHandler()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, RightArrow.transform);
        RightArrow.AddInteractionPunch();
        if (_moduleSolved)
            return false;
        if (wiresMoving)
            return false;
        if (_doorOpen)
        {
            if (wiresPosition == 0)
            {
                StartCoroutine(Move(WireParent.transform, 0f, -0.063f, 0f, 0f));
                wiresPosition = 1;
            }
            else if (wiresPosition == 1)
            {
                StartCoroutine(Move(WireParent.transform, -0.063f, -0.126f, 0f, 0f));
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
            WireDisabling();
            yield return null;
            elapsed += Time.deltaTime;
        }
        obj.localPosition = new Vector3(endPosX, posY, posX);
        wiresMoving = false;
    }
}
