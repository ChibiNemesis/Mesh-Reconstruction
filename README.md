# Mesh Reconstruction - Mesh deformations using XR-PBD To create patient specific models for medical XR applications

[![Unity Version](https://img.shields.io/badge/Unity-2022.3%2B-blue.svg)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Overview
Mesh Reconstruction is a procedural geometry pipeline designed for Extended Reality (XR) medical simulations. It bridges the gap between raw clinical DICOM data and real-time physics engines. Instead of relying on excessively dense Marching Cubes reconstructions, this tool utilizes **Centroid-Directed Ray Casting** and **Barycentric Interpolation** to seamlessly morph pre-optimized, physics-ready anatomical templates into patient-specific models.

**Key Features:**
* Low memory footprint (sub-4MB) ideal for XR soft-body physics (e.g., XR-PBD).
* Maintains consistent `< 3,000` vertex topologies.
* Mathematically handles up to 20% missing clinical data via Quadratic Bézier interpolation.

---

## Visual Results
> **Note:** *[Suggestion: Put a side-by-side image or a short GIF here showing a generic bone morphing into a patient-specific bone. Visual proof immediately hooks the reader.]*

![Deformation Demo](Link-to-your-image-or-gif.gif)

---

## Getting Started

### Prerequisites
* **Unity Version:** 2022.3.45
* **Dependencies:** MAGES NXT, XR-PBD package from MAGES NXT, delaunator-c-sharp

### Running the Pre-Built Examples
To see the pipeline in action, you can run the ready-made patient cases included in this repository.

1. Clone or download this repository.
2. Open the project in Unity (make sure not to update Mages NXT, if the editor asks, just in case for any weird behaviours).
3. Navigate to the `Assets/Scenes` folder.
4. Open the Example scene `Simple.unity`.
5. Go to `Window/Particle Grab Generator` and for each deformable object on the scene, initialize the Grabbers
6. Press **Play** in the Unity Editor.
7. Select any deformable object from the dropdown UI
8. Click the **Reconstruct** button in the UI to execute the deformation pipeline and watch the template map to the target contours.

---

## Setting Up a Custom Example (From Scratch)
You can easily apply this pipeline to your own templates and contour datasets. Follow these steps to set up a new example:

### 1. Import Your Assets
Import your initial 3D template into the scene and initialize

### 2. Attach the Pipeline Controller
Create an empty GameObject and attach the `Bounds Slicer`, `Bounds Slice Visualizer` component to it.

![Inspector Setup](Link-to-your-inspector-screenshot.png)
*Caption: The main controller script attached to a GameObject.*

### 3. Assign References
In the Unity Inspector, assign your initial template to the **[Insert Field Name]** slot, and drop your contour data into the **[Insert Field Name]** array.

![Assigning References](Link-to-your-references-screenshot.png)
*Caption: Dragging and dropping the template and contours into the script.*

### 4. Configure Mapping Settings
Adjust the parameters for the deformation:
* **Missing Data Tolerance:** Set how the pipeline handles gaps.
* **Barycentric Cap %:** Define the percentage of the distal ends to be mapped using Barycentric coordinates to prevent artifacting.

![Parameter Setup](Link-to-your-parameters-screenshot.png)
*Caption: Configuring the Bézier and Barycentric parameters.*

### 5. Execute
Press **Play** in the editor. You can use the provided debug gizmos to visually verify the ray cast trajectories and interpolation curves in the Scene view before finalizing the mesh!

---

## Documentation and Implementation Details
This README focuses on usage and results. For a deep dive into the mathematical implementation, ray cast logic, and missing data handling, please refer to the fully commented C# scripts within the `Assets/Scripts` folder, or read the full thesis report here: [Link to PDF if applicable].

## Citation
If you use this pipeline in your research, please cite:
```bibtex
@mtzpdef{zpdeform2026,
  author  = {Ziotas Paul},
  title   = {Mesh deformations using XR-PBD To create patient specific models for medical XR applications},
  school  = {University of Crete},
  year    = {2026}
}
