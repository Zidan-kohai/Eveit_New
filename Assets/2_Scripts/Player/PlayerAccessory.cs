using GeekplaySchool;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAccessory : MonoBehaviour
{

    [SerializeField] private List<Accessory> headphonesSkine;


    private void Start()
    {
        StartCoroutine(Wait(0.3f, WearSkine));
    }

    private void WearSkine()
    {
        foreach (var item in headphonesSkine)
        {
            if(item.index == Geekplay.Instance.PlayerData.CurrentEquipedAccessoryID)
            {
                item.gameObject.SetActive(true);
                item.Event?.Invoke();
            }
            else
            {
                item.gameObject.SetActive(false);
            }
        }
    }


    private IEnumerator Wait(float time, Action action)
    {
        yield return new WaitForSeconds(time);

        action?.Invoke();
    }

}