# Project Structure

This repository is organized around a SolidWorks API learning workflow for copper busbar automation.

## `C#/TopToDown`

Main demo project.

Responsibilities:

- Connect to the active SolidWorks session.
- Read the active assembly document.
- Scan assembly components and extract named reference points.
- Build busbar centerline routes.
- Create 3D path sketches and rectangular sweep profiles.
- Generate copper busbar solid parts.
- Save generated parts and insert them into the assembly.

Important file:

```text
C#/TopToDown/TopToDown/Program.cs
```

## `C#/FeatureExtract`

Standalone diagnostic tool.

Responsibilities:

- Connect to the active SolidWorks session.
- Scan the current part or assembly.
- Print feature names, feature types, reference point names, and coordinates.

This tool is useful when a model does not route correctly and the first step is to verify what the API can actually see.

## `C#/.../ReferenceDLL`

Local SolidWorks interop references.

These DLLs are intentionally kept in the repository so the sample projects can be opened more easily on the same SolidWorks/API version family.

## `SWtopToDown`

Sample SolidWorks models used for development and testing.

Tracked files include the small test assembly, fuse part, leakage breaker part, mounting plate, and skeleton part.

Generated busbar files named like this are ignored:

```text
Busbar_*.SLDPRT
```

## `docs`

Project documentation and workflow notes.

## Root Data Files

```text
配电箱二次开发_数据层模板.xlsx
配电箱二次开发路线图.md
```

These files document the learning roadmap and early data-layer ideas. The current geometry implementation still prioritizes running the SolidWorks modeling workflow end to end.
