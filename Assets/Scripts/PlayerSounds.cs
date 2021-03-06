﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//---------------------------------------------------------------------------------------
// Copyright © Janne Isoaho, Aarne Manneri, Mikael Myyrä, Lauri Niskanen, Saska Sinkkonen
//---------------------------------------------------------------------------------------

public class PlayerSounds : MonoBehaviour {
    [Header("Audio Clips")]
    [SerializeField] private AudioClip acceleration;
    [SerializeField] private AudioClip deceleration;
    [SerializeField] private AudioClip regularDriveFast;
    [SerializeField] private AudioClip regularDriveSlow;
    [SerializeField] private AudioClip crash;
    [SerializeField] private AudioClip crashGround;
    
    [Header("Crossfade")]
    [SerializeField] private float fadeDuration;
    [SerializeField] private float crossfadeVolume;
    public float CrossfadeVolume
    {
        get { return crossfadeVolume; }
        set { crossfadeVolume = value; }
    }
    private AudioSource[] aSources;
    private IEnumerator[] fader = new IEnumerator[2];
    private int activeSourceIndex = 0;
    private int volumeChangesPerSecond = 15;
    
    [Header("Other")]
    [SerializeField] private float accelerationThreshold;
    [SerializeField] private float speedThreshold;
    [SerializeField] private float collisionWaitTime;
    private float previousSpeed;
    private float speed;
    private float speedDiff;
    private float collisionTimer;
    private float randomPitch;
    private bool crashedRecently;
    private Vector3 horizontalVelocity;
    private Rigidbody rb;
    private AudioSource aSourceDefault;

    private float sameAudioTimer;
    private bool sameAudioTimerStarted;

    //todo audiocheck threshold sen hetkisestä nopeudesta riippuvaiseksi?
    //todo kiihdytys ääni pois jos nopeus liian hidas?
    //todo ehkä invokerepeating pois ja fixedupdateen checkaudio? Tai invokerepeating väli pienemmäksi
    //todo nyt kiihdytys ja hidastuskin (vaikka threshold/2) loppuu "liian aikaisin". Kuulostaa paremmalta jos kiihdytyksessä sen ääni kuuluu vielä vähän vaikka nopeus on jo maksimissa
    //myös törmäyksessä hidastus pitäisi soittaa kerran läpi
    void CheckAudio()
    {
        horizontalVelocity = rb.velocity - new Vector3(0, rb.velocity.y, 0);
        speed = rb.velocity.magnitude;
        speedDiff = speed - previousSpeed;

        if (speedDiff < -accelerationThreshold / 2) Play(deceleration);
        else if (speedDiff > accelerationThreshold && speed > 0) Play(acceleration);
        else
        {
            if (speed < speedThreshold) Play(regularDriveSlow);
            else Play(regularDriveFast);
        }

        previousSpeed = speed;
    }

    private void Awake()
    {
        aSourceDefault = gameObject.GetComponent<AudioSource>();

        //Tehdään kaksi audioSourcea joiden avulla crossfade
        aSources = new AudioSource[2]{ gameObject.AddComponent<AudioSource>(), gameObject.AddComponent<AudioSource>() };
        
        foreach (AudioSource s in aSources)
        {
            s.loop = true;
            s.playOnAwake = false;
            s.volume = 0.0f;
        }

        rb = GetComponent<Rigidbody>();
        InvokeRepeating("CheckAudio", 1f, 0.2f);
    }

    void Update()
    {
        if (crashedRecently)
        {
            collisionTimer += Time.deltaTime;
            if (collisionTimer > collisionWaitTime)
            {
                crashedRecently = false;
                collisionTimer = 0;
            }
        }
        
        //Jos samaa auton ääntä soitettu pitkään, muutetaan sitä vähän
        sameAudioTimer += Time.deltaTime;
        if (sameAudioTimer > 2)
        {
            randomPitch = Random.Range(0.98f, 1.02f);
            StartCoroutine(LerpPitch(aSources[activeSourceIndex], 1, randomPitch));
            StartSameAudioTimer();
        }
    }
    
    void OnCollisionEnter(Collision other)
    {
        //Lasketaan törmäyksen kulma
        Vector3 relpos = (other.contacts[0].point - gameObject.transform.position).normalized;
        Vector3 vel = -other.relativeVelocity.normalized;
        float angle = Vector3.Angle(relpos, vel);

        if (!crashedRecently)
        {
            //Muuttaa volumea törmäyksen kulman mukaan - todo muuttujat randomlukujen tilalle
            aSourceDefault.volume = Mathf.Clamp((5/angle), 0, 1) * Mathf.Clamp((other.relativeVelocity.magnitude/40), 0, 1);

            if (other.collider.gameObject.CompareTag("Ground"))
            {
                aSourceDefault.PlayOneShot(crashGround);
            }
            else
            {
                aSourceDefault.volume = 0.2f * aSourceDefault.volume; //Tämä vaan kun ei jaksa muokata seinääntörmäysclippiä hiljasemmaks
                aSourceDefault.PlayOneShot(crash);
            }
            crashedRecently = true;
        }

    }

    //Vaihtaa soitettavan clipin. Häivyttää vanhan pois ja uuden sisään
    public void Play(AudioClip clip)
    {
        if (clip == aSources[activeSourceIndex].clip)
        {
            return;
        }

        //Lopettaa kaikki Coroutinet
        foreach (IEnumerator i in fader)
        {
            if (i != null)
            {
                StopCoroutine(i);
            }
        }

        //Feidaa pois aktiivisen audion
        if (aSources[activeSourceIndex].volume > 0)
        {
            fader[0] = FadeAudioSource(aSources[activeSourceIndex], fadeDuration, 0.0f, () => { fader[0] = null; });
            StartCoroutine(fader[0]);
        }

        //Feidaa sisään uuden audion
        int NextPlayer = (activeSourceIndex + 1) % aSources.Length;
        aSources[NextPlayer].clip = clip;
        aSources[NextPlayer].Play();
        StartSameAudioTimer();

        fader[1] = FadeAudioSource(aSources[NextPlayer], fadeDuration, CrossfadeVolume, () => { fader[1] = null; });
        StartCoroutine(fader[1]);
        
        activeSourceIndex = NextPlayer;
    }
    
    //Feidaa audion sen hetkisestä äänenvoimakkuudesta targetVolumeen ajassa duration
    IEnumerator FadeAudioSource(AudioSource player, float duration, float targetVolume, System.Action finishedCallback)
    {
        int Steps = (int)(volumeChangesPerSecond * duration);
        float StepTime = duration / Steps;
        float StepSize = (targetVolume - player.volume) / Steps;

        //Lisää ääntä StepSizen verran ja odottaa StepTimen
        for (int i = 1; i < Steps; i++)
        {
            player.volume += StepSize;
            yield return new WaitForSeconds(StepTime);
        }
        player.volume = targetVolume;

        //Callback
        if (finishedCallback != null)
        {
            finishedCallback();
        }
    }

    //Muuttaa audioSourcen pitchiä pehmeästi
    IEnumerator LerpPitch(AudioSource player, float duration, float targetPitch)
    {
        int Steps = (int)(volumeChangesPerSecond * duration);
        float StepTime = duration / Steps;
        float StepSize = (targetPitch - player.pitch) / Steps;

        for (int i = 1; i < Steps; i++)
        {
            player.pitch += StepSize;
            yield return new WaitForSeconds(StepTime);
        }
        player.pitch = targetPitch;
    }

    //Laskee, kuinka pitkään samaa auton ääntä soitettu
    void StartSameAudioTimer()
    {
        sameAudioTimer = 0;
    }
}
