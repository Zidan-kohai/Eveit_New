using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DailyExercise
{
    [SerializeField] private DailyExerciseData data;
    private List<ExerciseProgress> exercises = new List<ExerciseProgress>();
    
    public void Init()
    {
        int exerciseCount = data.ExerciseCount();

        for (int i = 0; i < exerciseCount; i++)
        {
            ExerciseInfo exerciseInfo = data.GetExerciseInfo(i);

            ExerciseProgress exercise = new ExerciseProgress();

            exercise.SetMaxProgress = exerciseInfo.MaxProgress;
            exercise.Description = exerciseInfo.GetDescription();
            exercise.Reward = exerciseInfo.RewardCount;

            exercises.Add(exercise);
        }
    }

    public int SetProgress(int exerciseNumber, int progress = 1)
    {
        return exercises[exerciseNumber].SetProggres(progress);
        
    }

    public void IsDone()
    {

    }

    public ExerciseProgress GetExercise(int exerciseNumber)
    {
        return exercises[exerciseNumber];
    }
}
