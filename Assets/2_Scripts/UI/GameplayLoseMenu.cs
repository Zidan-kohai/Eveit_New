using System;
using TMPro;
using UnityEngine;

public class GameplayLoseMenu : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI survivedTimeView;
    [SerializeField] private TextMeshProUGUI earnGoldTimeView;

    public void Show(float survivedTime)
    {
        gameObject.SetActive(true);
        TimeSpan timeSpan = TimeSpan.FromSeconds(survivedTime);
        survivedTimeView.text = string.Format("{0:D2}:{1:D2}", timeSpan.Minutes, timeSpan.Seconds);
    }
}
