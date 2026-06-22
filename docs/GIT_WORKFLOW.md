# Git Workflow Notes

This is a beginner-friendly workflow for this project.

## Basic Idea

Git is the local version history.

GitHub is the remote copy of that history.

You usually work in this order:

```text
edit files -> check status -> add files -> commit locally -> push to GitHub
```

## Common Commands

Check what changed:

```powershell
git status
```

Stage changed files:

```powershell
git add .
```

Create a local save point:

```powershell
git commit -m "Describe the change"
```

Upload commits to GitHub:

```powershell
git push
```

Download changes from GitHub:

```powershell
git pull
```

## What Should Be Committed

Good candidates:

- C# source code
- project files
- documentation
- Excel configuration templates
- small sample SolidWorks models

Usually ignored:

- `bin/`
- `obj/`
- `.vs/`
- generated busbar parts
- temporary SolidWorks files
- large demo videos

## SolidWorks File Notes

SolidWorks model files are binary. Git can store them, but it cannot show detailed text differences.

For learning and sample models, Git is acceptable.

For large production projects or multi-user CAD work, use a proper CAD/PDM workflow such as SolidWorks PDM.
