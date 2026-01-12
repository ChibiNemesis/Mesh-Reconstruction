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

    private List<bool> WireFrameToggles;

    void Start()
    {
        FillList();
    }

    //Method that fills the content list of the UI
    private void FillList()
    {
        if (ContentRect == null || ListButton == null)
        {
            Debug.LogWarning("UI references missing.");
            return;
        }

        var deformables = FindObjectsByType<SliceReshaper>(FindObjectsSortMode.None);

        Reshapers = new List<SliceReshaper>(deformables.Length);
        ReshaperButtons = new List<Button>(deformables.Length);
        WireFrameToggles = new List<bool>(deformables.Length);

        foreach (var def in deformables)
        {
            var btn = Instantiate(ListButton);
            btn.transform.SetParent(ContentRect.transform, false);

            var text = btn.transform.GetChild(0).GetComponent<TMP_Text>();
            text.text = def.gameObject.name;

            Reshapers.Add(def);
            ReshaperButtons.Add(btn.GetComponent<Button>());
            WireFrameToggles.Add(false);

            var reshaperRef = def;
            btn.GetComponent<Button>().onClick.AddListener(() => OnButtonPress(reshaperRef));
        }

        // Resize Content
        var contentRT = ContentRect.GetComponent<RectTransform>();

        // Force Unity layout system to apply vertical layout and content size
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);

        //Possible fix for list
        /*
         // 1. Force the Content Size Fitter to calculate the new height immediately
        var contentRT = ContentRect.GetComponent<RectTransform>();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
        
        // 2. Reset the Scroll View to the TOP
        // If ScrollArea has the ScrollRect component:
        var scrollRect = ScrollArea.GetComponent<ScrollRect>(); 
        if (scrollRect != null)
        {
            // Stop any momentum from previous scrolls
            scrollRect.velocity = Vector2.zero; 
            
            // Set position to Top (1 = Top, 0 = Bottom)
            scrollRect.verticalNormalizedPosition = 1f; 
        }
         */
    }

    private void OnButtonPress(SliceReshaper reshaper)
    {
        if (SelectedBtn != null)
            SelectedBtn.interactable = true;

        int Index = Reshapers.IndexOf(reshaper);

        SelectedBtn = ReshaperButtons[Index];
        SelectedBtn.interactable = false;

        SelectedReshaper = Reshapers[Index];

        // Update Sample Method Text
        var initializer = SelectedReshaper.GetComponent<SliceInitializer>();

        // Sync toggles (TEMPORARILY DISABLE EVENTS)
        WireFrameToggle.onValueChanged.RemoveAllListeners();
        InterpolateToggle.onValueChanged.RemoveAllListeners();

        WireFrameToggle.isOn = SelectedReshaper.GetWireFrame();
        InterpolateToggle.isOn = SelectedReshaper.InterpolatedDeformation;

        WireFrameToggle.onValueChanged.AddListener((_) => OnWireFrameToggle());
        InterpolateToggle.onValueChanged.AddListener((_) => InterpolationToggle());

        // Buttons states
        SaveBtn.interactable = SelectedReshaper.GetIsFinished();
        ReshapeBtn.interactable = !SelectedReshaper.GetLock();

        Camera.main.gameObject.transform.LookAt(reshaper.gameObject.transform.position);
    }

    //Change Selected Object's material (normal or wireframe)
    public void OnWireFrameToggle()
    {
        Debug.Assert(WireFrameToggle != null);
        Debug.Assert(WireMat != null);

        SelectedReshaper.SetWireframe(WireFrameToggle.isOn, WireMat);
    }

    public void InterpolationToggle()
    {
        Debug.Assert(InterpolateToggle!=null);
        if (SelectedReshaper != null)
        {
            SelectedReshaper.SetInterpolation(InterpolateToggle.isOn);
        }
    }

    //Reshape Selected object
    public void ReshapeSelected()
    {
        Debug.Assert(SelectedReshaper != null);
        SelectedReshaper.DeformSlices();
        ReshapeBtn.interactable = false;
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
