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
    }
}
