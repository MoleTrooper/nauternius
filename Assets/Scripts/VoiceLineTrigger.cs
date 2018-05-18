﻿using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

//---------------------------------------------------------------------------------------
// Copyright © Janne Isoaho, Aarne Manneri, Mikael Myyrä, Lauri Niskanen, Saska Sinkkonen
//---------------------------------------------------------------------------------------

public class VoiceLineTrigger : MonoBehaviour {
    
    // purkkaviritelmä/5
    [SerializeField] private AudioClip clip1;
    [SerializeField] private AudioClip clip2;
    [SerializeField] private float waitBetweenAudio;
    [SerializeField] private List<string> sentences = new List<string>();
    [SerializeField] private List<int> sentenceLengths = new List<int>();
    [SerializeField] private List<string> sentences2 = new List<string>();
    [SerializeField] private List<int> sentenceLengths2 = new List<int>();

    [SerializeField] private Text voiceLineText;
    [SerializeField] private GameObject aSourceObj;
    private AudioSource aSource;
    private CheckIfFree textInUseCheck;

    public bool Triggered { get; set; }
    private CanvasGroup canvasGroup;
    private List<VoiceLineTrigger> sameVoiceLine = new List<VoiceLineTrigger>();
    
    private void Start()
    {
        textInUseCheck = voiceLineText.GetComponent<CheckIfFree>();
        aSource = aSourceObj.GetComponent<AudioSource>();
        canvasGroup = voiceLineText.transform.parent.GetComponent<CanvasGroup>();

        //Lisää listaan kaikki lapsi-skriptit eli skriptit, joissa sama voiceline eri triggerboxissa
        if (transform.parent != null)
        {
            foreach (VoiceLineTrigger vcl in transform.parent.GetComponentsInChildren<VoiceLineTrigger>()) sameVoiceLine.Add(vcl);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !Triggered)
        {
            foreach (VoiceLineTrigger vlc in sameVoiceLine) vlc.Triggered = true;
            StartCoroutine(PlayClips());
        }
    }

    IEnumerator PlayClips()
    {
        aSource.clip = clip1;
        aSource.Play();
        //aSource.PlayOneShot(clip1);
        StartCoroutine(TypeText(sentences, sentenceLengths));
        yield return new WaitForSeconds(waitBetweenAudio + clip1.length);
        if (clip2 != null)
        {
            //aSource.PlayOneShot(clip2);
            aSource.clip = clip2;
            aSource.Play();
            StartCoroutine(TypeText(sentences2, sentenceLengths2));
        }
    }

    //Kirjoittaa tekstiä lause kerrallaan näytölle
    IEnumerator TypeText(List<string> sentences, List<int> sentenceLengths)
    {
        textInUseCheck.StartText(this);

        canvasGroup.alpha = 1f;
        voiceLineText.text = "";
        for (int i = 0; i < sentences.Count; i++)
        {
            voiceLineText.text = sentences[i];
            if (i == sentences.Count - 1) StartCoroutine(FadeCanvas(sentenceLengths[i]));
            yield return new WaitForSeconds(sentenceLengths[i]);
        }
    }

    //Odottaa vähän ja alkaa feidaamaan canvasta
    public IEnumerator FadeCanvas(float startWait)
    {
        for (int i = 0; i < 20; i++)
        {
            if (i == 0) yield return new WaitForSeconds(startWait);
            canvasGroup.alpha -= 0.05f;
            yield return new WaitForSeconds(0.05f);
        }
        textInUseCheck.StopText(); //Kerrotaan että tekstiobjekti vapaa
    }

}
