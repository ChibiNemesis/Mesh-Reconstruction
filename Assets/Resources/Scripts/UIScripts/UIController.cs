using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UIController : MonoBehaviour
{
    //Gameobject That will contain a button for each deformable object
    [SerializeField]
    GameObject ContentRect;

    //Used to fit scrollArea to desirable size
    [SerializeField]
    RectTransform ScrollArea;

    [SerializeField]
    GameObject ListButton;

    [SerializeField]
    TMP_Text SampleMethod;

    void Start()
    {
        FillList();
    }

    //Method that fills the content list of the UI
    private void FillList()
    {
        if (ContentRect == null)
        {
            Debug.LogWarning("Did not Find ContentRect Reference");
            return;
        }
        if(ListButton == null)
        {
            Debug.LogWarning("Did not Find ListButton Reference");
            return;
        }

        var Deformables = FindObjectsByType<SliceReshaper>(FindObjectsSortMode.None);

        foreach(var def in Deformables)
        {
            var btn = Instantiate(ListButton);
            btn.transform.parent = def.gameObject.transform;
            var text = btn.transform.GetChild(0).transform.GetComponent<TMP_Text>();
            Debug.Assert(text != null);
            text.text = (def.gameObject.name);
        }

        var BtnHeight = ListButton.GetComponent<RectTransform>().rect.height;

        //Modify the scrollArea to match the buttons count
        ScrollArea.sizeDelta = new Vector2(ScrollArea.rect.width, Deformables.Length * BtnHeight);
    }
}
