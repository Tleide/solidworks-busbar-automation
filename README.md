# SolidWorks Busbar Automation

A C# SolidWorks API learning project for automated copper busbar routing and generation in electrical distribution cabinet assemblies.

This repository explores a top-down workflow where named terminal reference points in a SolidWorks assembly are detected automatically, converted into assembly coordinates, and used to generate routed copper busbar parts.

## Project Status

This is an experimental learning project, not a production-ready electrical design tool.

Current focus:

- SolidWorks API environment setup
- Assembly feature scanning and reference point extraction
- Automatic route generation from fuse terminals to leakage breaker terminals
- Prototype generation of main feed busbars, collector busbars, and branch busbars
- Parameterized busbar width, thickness, phase spacing, and lap-side logic

## Main Features

- Connects to an active SolidWorks session through COM automation.
- Scans assembly components for named reference points such as `A_OUT`, `B_OUT`, `C_OUT`, `A_IN`, `B_IN`, and `C_IN`.
- Converts component-local reference point coordinates into assembly-global coordinates.
- Identifies fuse and leakage breaker components by component name hints.
- Generates routed 3D sketch paths for busbar centerlines.
- Creates rectangular sweep profiles for copper busbar solids.
- Saves generated busbar parts and inserts them back into the active assembly.
- Closes generated part documents after insertion to avoid too many open SolidWorks windows.

## Repository Layout

```text
.
├── C#/
│   ├── TopToDown/              # Main SolidWorks busbar generation demo
│   └── FeatureExtract/         # Standalone feature/reference-point extraction tool
├── SWtopToDown/                # Sample SolidWorks assembly and part files
├── docs/                       # Project notes and workflow documentation
├── demo/                       # Demo media and preview images
├── 配电箱二次开发_数据层模板.xlsx
└── 配电箱二次开发路线图.md
```

See [docs/PROJECT_STRUCTURE.md](docs/PROJECT_STRUCTURE.md) for more details.

## Requirements

- Windows
- SolidWorks installed and registered as a COM server
- Visual Studio 2022 or later
- .NET Framework 4.8
- Git, if you want to version-control or push changes to GitHub

The project uses SolidWorks interop assemblies stored in the local `ReferenceDLL` folders.

## Quick Start

1. Open SolidWorks.
2. Open the sample assembly in `SWtopToDown/`.
3. Open `C#/TopToDown/TopToDown.slnx` in Visual Studio.
4. Build the `TopToDown` project.
5. Run the console application.
6. Watch the console output for detected reference points and generated busbar routes.

The program expects the target assembly to be the active SolidWorks document.

## Reference Point Naming

The current route logic expects terminal reference points to use this naming style:

```text
A_OUT
B_OUT
C_OUT
A_IN
B_IN
C_IN
```

Typical meaning:

- `*_OUT`: outgoing terminals on the fuse or upstream component
- `*_IN`: incoming terminals on the leakage breaker or downstream component

## Important Notes

- SolidWorks files such as `.SLDPRT` and `.SLDASM` are binary files. Git can store them, but it cannot show line-by-line changes like it can for C# code.
- Generated busbar parts named `Busbar_*.SLDPRT` are ignored by Git by default.
- Build output folders such as `bin/`, `obj/`, and Visual Studio `.vs/` folders are ignored.
- Large production CAD models should normally be managed by a CAD/PDM workflow. This repository keeps only small sample models for development and testing.

## Development Workflow

For a beginner-friendly Git workflow, see [docs/GIT_WORKFLOW.md](docs/GIT_WORKFLOW.md).

## License

This project is currently intended for personal study and experimentation. Add a formal license before using it in a public or commercial context.
