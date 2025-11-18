using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

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

    [SerializeField]
    Toggle WireFrameToggle;

    [SerializeField]
    Toggle InterpolateToggle;

    [SerializeField]
    Button ReshapeBtn;

    [SerializeField]
    Button SaveBtn;

    [SerializeField]
    Material WireMat;

    private SliceReshaper SelectedReshaper;
    private Button SelectedBtn;

    private List<SliceReshaper> Reshapers;
    private List<Button> ReshaperButtons;

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
        Reshapers = new List<SliceReshaper>();
        ReshaperButtons = new List<Button>();

        foreach(var def in Deformables)
        {
            var btn = Instantiate(ListButton);
            btn.transform.SetParent(ContentRect.transform);
            var text = btn.transform.GetChild(0).transform.GetComponent<TMP_Text>();
            Debug.Assert(text != null);
            text.text = (def.gameObject.name);
            Reshapers.Add(def);
            ReshaperButtons.Add(btn.GetComponent<Button>());

            int Index = Reshapers.Count - 1;

            btn.GetComponent<Button>().onClick.AddListener(() => { OnButtonPress(Index); });
        }

        var BtnHeight = ListButton.GetComponent<RectTransform>().rect.height;

        //Modify the scrollArea to match the buttons count
        ScrollArea.sizeDelta = new Vector2(ScrollArea.rect.width, Deformables.Length * BtnHeight);
        //Modify content also, probably
    }

    private void OnButtonPress(int Index)
    {
        if (SelectedBtn != null)
        {
            SelectedBtn.interactable = true;
        }

        var btn = ReshaperButtons[Index];

        this.SelectedBtn = btn;
        this.SelectedBtn.interactable = false;
        var BtnIndex = ReshaperButtons.IndexOf(btn.GetComponent<Button>());
        SelectedReshaper = Reshapers[BtnIndex];

        var res = Reshapers[Index];

        //setup sampling method
        var initializer = res.gameObject.GetComponent<SliceInitializer>();
        if (initializer != null)
        {
            if (initializer.SamplingMethod == SliceInitializer.SamplingMode.UNIFORM)
            {
                SampleMethod.text = "Uniform";
            }
            else if (initializer.SamplingMethod == SliceInitializer.SamplingMode.RANDOMIZED)
            {
                SampleMethod.text = "Randomized";
            }
            else
            {
                SampleMethod.text = "None";
            }
        }
        else
        {
            SampleMethod.text = "None";
        }

        //Setup Toggles
        this.InterpolateToggle.isOn = SelectedReshaper.InterpolatedDeformation;
        this.WireFrameToggle.isOn = SelectedReshaper.GetWireFrame(); ;

        //Setup Buttons
        SaveBtn.interactable = SelectedReshaper.GetIsFinished();
        ReshapeBtn.interactable = !SelectedReshaper.GetIsFinished();
    }

    //Change Selected Object's material (normal or wireframe)
    public void OnWireFrameToggle()
    {
        Debug.Assert(WireFrameToggle != null);
        Debug.Assert(WireMat != null);
        var rend = SelectedReshaper.gameObject.GetComponent<MeshRenderer>();

        //Do this inside slicereshaper
        /*for(int m = 0; m < rend.materials.Length; m++)
        {
            rend.materials[m] = WireMat;
        }*/
        SelectedReshaper.SetWireframe(!SelectedReshaper.GetWireFrame());
        this.WireFrameToggle.isOn = SelectedReshaper.GetWireFrame(); ;
    }

    public void InterpolationToggle()
    {
        Debug.Assert(InterpolateToggle!=null);
        if (SelectedReshaper != null)
        {
            SelectedReshaper.SetInterpolation(!SelectedReshaper.GetInterpolation());
            this.InterpolateToggle.isOn = SelectedReshaper.InterpolatedDeformation;
        }
    }

    //Reshape Selected object
    public void ReshapeSelected()
    {
        Debug.Assert(SelectedReshaper != null);
        SelectedReshaper.DeformSlices();
    }

    //Save Selected mesh (if it is deformed first)
    public void SaveSelected()
    {
        if (SelectedReshaper.GetIsFinished())
        {
            Debug.Log("Save Model");
        }
    }
}
