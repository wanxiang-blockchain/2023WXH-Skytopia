using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoodAdapter : MonoBehaviour
{

    
    
    public static int Param_Mood = Animator.StringToHash("Mood");
    public static int Param_Talking = Animator.StringToHash("Talking");

    public List<MoodParam> Moods;

    public Animator _Animator;

    private void Start()
    {

    }
}



public enum MoodType
{
    Laugh = 1,
    Surprise,
    Cry,
    Question,
    Angry,
    Afraid
}