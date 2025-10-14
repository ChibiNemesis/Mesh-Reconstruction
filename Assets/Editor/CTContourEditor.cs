using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class CTContourEditor : EditorWindow
{
    private VisualElement m_RightPane;
    [SerializeField] private int m_SelectedIndex = -1;

    [MenuItem("Window/UI Toolkit/CTContourEditor")]
    public static void ShowExample()
    {
        CTContourEditor wnd = GetWindow<CTContourEditor>();
        wnd.titleContent = new GUIContent("CT Contour Editor");
    }

    public void CreateGUI()
    {
        //var allObjectGuids = AssetDatabase.FindAssets("a:assets t:Sprite glob:Resources/CT/Kidney");
        string[] allObjectGuids = AssetDatabase.FindAssets("t:texture2D", new[] { "Assets/Resources/CT/Kidney" });

        foreach (string guid2 in allObjectGuids)
        {
            Debug.Log(AssetDatabase.GUIDToAssetPath(guid2));
        }

        var allObjects = new List<Sprite>();
        foreach (var guid in allObjectGuids)
        {
            allObjects.Add(AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // Create a two-pane view with the left pane being fixed.
        var splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);

        // Add the view to the visual tree by adding it as a child to the root element.
        rootVisualElement.Add(splitView);

        // A TwoPaneSplitView always needs two child elements.
        var leftPane = new ListView();
        splitView.Add(leftPane);
        m_RightPane = new ScrollView(ScrollViewMode.VerticalAndHorizontal);

        splitView.Add(m_RightPane);

        // Initialize the list view with all sprites' names.
        leftPane.makeItem = () => new Label();
        leftPane.bindItem = (item, index) => { (item as Label).text = allObjects[index].name; };
        leftPane.itemsSource = allObjects;

        // React to the user's selection.
        leftPane.selectionChanged += OnSpriteSelectionChange;

        // Restore the selection index from before the hot reload.
        leftPane.selectedIndex = m_SelectedIndex;

        // Store the selection index when the selection changes.
        leftPane.selectionChanged += (items) => { m_SelectedIndex = leftPane.selectedIndex; };
    }

    private void OnSpriteSelectionChange(IEnumerable<object> selectedItems)
    {
        // Clear all previous content from the pane.
        m_RightPane.Clear();

        var enumerator = selectedItems.GetEnumerator();
        if (enumerator.MoveNext())
        {
            var selectedSprite = enumerator.Current as Sprite;
            if (selectedSprite != null)
            {
                // Add a new Image control and display the sprite.
                var spriteImage = new Image();
                spriteImage.scaleMode = ScaleMode.StretchToFill;
                spriteImage.sprite = selectedSprite;

                // Add the Image control to the right-hand pane.
                m_RightPane.Add(spriteImage);
            }
        }
    }
}
