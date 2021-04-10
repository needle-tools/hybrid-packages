# UPM Packages in .unitypackage files

Unity has two separate package formats:
- `.unitypackage`, a zip-based file format that is used on the Asset Store
- `UPM package`, to prepare modules for Unity's package manager.
  
The latter is newer, enforces better code and directory structure (AsmDefs required), and generally much easier to work with / add / remove / update.

So far, it has been impossible to ship UPM packages on Asset Store or via `.unitypackage` files. This repository fixes that by
- allowing packages to be uploaded to the Asset Store through the regular Asset Store Tools
- enabling <kbd>Assets/Export Package</kbd> to export stuff from package folders directly

> :warning: Please be aware that Unity might decide to disable this functionality at any point, and will likely add some form of UPM support to AssetStore in the next years. This is a pretty experimental solution.

## Installation 💾
1. 
    <details>
    <summary>Add OpenUPM with the <code>com.needle</code> scope to your project (this package has a dependency there)</summary>

    - open <kbd>Edit/Project Settings/Package Manager</kbd>
    - add a new Scoped Registry:
    ```
    Name: OpenUPM
    URL:  https://package.openupm.com/
    Scope(s): com.needle
    ```
    - click <kbd>Save</kbd>
    </details>
2. Add this repository as git package (it's not on OpenUPM yet)
   - open <kbd>Window/Package Manager</kbd>
   - click <kbd>+</kbd>
   - click <kbd>Add package from git URL</kbd>
   - paste `https://github.com/needle-tools/upm-in-unitypackage.git/?path=/package`
   - click <kbd>Add</kbd>

## How to use 💡

### Export a .unitypackage that contains files in Packages or entire packages
As usual, select what you want to export, and hit <kbd>Assets/Export Package</kbd> (also available via right click).  

### Upload Packages to Asset Store
1. Install the Asset Store Tools as usual: https://assetstore.unity.com/packages/tools/utilities/asset-store-tools-115
1. Open <kbd>Asset Store Tools/Package Upload</kbd>
1. Press <kbd>Select</kbd> and select a local or embedded package
2. Make sure "include dependencies" is off - you can now specify them through your package.json!
3. Press <kbd>Upload</kbd>

## Known Issues / Limitations
- The optional <kbd>Validate</kbd> step isn't supported yet. Export your package via <kbd>Right Click/Export Package</kbd> and test in a separate project for now.
- If you run into any issues, you can temporarily disable the functionality via <kbd>Edit/Project Settings/Needle/Editor Patch Manager</kbd> or remove the package. Please report a bug!
- The `package.json` of your package can [define dependencies](https://docs.unity3d.com/Manual/upm-manifestPrj.html). However, only dependencies from the Unity Registry will be automatically resolved in empty projects - we'll need to think of a separate mechanism / guidance for people to add dependencies from scoped registries. For now, it's recommended that you guard against missing dependencies via [Version Defines](https://docs.unity3d.com/Manual/ScriptCompilationAssemblyDefinitionFiles.html#define-symbols).

## Technical Details  
All the infrastructure is there to use packages in .unitypackages; it mostly looks like nobody has taken the time to revisit some decisions around this. It just works! It's just too many incorrect/outdated safety checks - so what we're doing here is bypassing those.

## Contact ✒️
<b>[🌵 needle — tools for unity](https://needle.tools)</b> • 
[@NeedleTools](https://twitter.com/NeedleTools) • 
[@marcel_wiessler](https://twitter.com/marcel_wiessler) • 
[@hybridherbst](https://twitter.com/hybridherbst) • 
[Needle Discord](https://discord.gg/CFZDp4b)