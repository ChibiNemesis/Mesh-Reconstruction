using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DropdownHandler : MonoBehaviour
{
    [SerializeField]
    GameObject Icon;

    [SerializeField]
    GameObject Content;


    public void ChangeState()
    {
        Content.SetActive(!Content.activeInHierarchy);
        /*if (Content.activeInHierarchy)
        {
            Icon.GetComponent<RectTransform>().rotation.z = 0;
        }
        else
        {
            Icon.GetComponent<RectTransform>().rotation.z = 90;
        }*/
    }
}
