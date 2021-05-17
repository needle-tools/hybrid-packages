# UPM Packages in .unitypackage files

![Unity Version Compatibility](https://img.shields.io/badge/Unity-2018.4%20%E2%80%94%202021.1-brightgreen)

Unity has two separate package formats:
- `.unitypackage`, a zip-based file format that is used on the Asset Store
- `UPM package`, to prepare modules for Unity's package manager.
  
The latter is newer, enforces better code and directory structure (AsmDefs required), and generally much easier to work with / add / remove / update.

So far, it has been impossible to ship UPM packages on Asset Store or via `.unitypackage` files.  
This package fixes that. After installing, you can select assets and folders from the Packages folder to be exported, which 
- allows packages to be uploaded to the Asset Store through the regular Asset Store Tools
- enables <kbd>Assets/Export Package</kbd> to export stuff from package folders directly

The resulting .unitypackage files _do not require any additional setup for users_. This package here is only required for creating those `.unitypackage` files, not for importing them.

> :warning: This is experimental. Unity will likely add some form of UPM support to AssetStore in the next years.

## Installation 💾
1. 
    <details>
    <summary>Add the OpenUPM registry with the <code>com.needle</code> scope to your project</summary>

    - open <kbd>Edit/Project Settings/Package Manager</kbd>
    - add a new Scoped Registry:
    ```
    Name: OpenUPM
    URL:  https://package.openupm.com/
    Scope(s): com.needle
    ```
    - click <kbd>Save</kbd>
    </details>
2. Add this package:
   - open <kbd>Window/Package Manager</kbd>
   - click <kbd>+</kbd>
   - click <kbd>Add package from git URL</kbd> or <kbd>Add package by name</kbd>
   - paste `com.needle.upm-in-unitypackage`
   - click <kbd>Add</kbd>

<details>
<summary><em>Alternative: git package (no PackMan updates, not recommended)</em></summary>  

- Complete step 1 above  
- Add `https://github.com/needle-tools/upm-in-unitypackage.git/?path=/package` in Package Manager  

</details>

## How to use 💡

### Export a .unitypackage that contains files in Packages or entire packages
1. select what you want to export
2. hit <kbd>Assets/Export Package</kbd> (also available via right click on Assets/Folders).  

### Upload Packages to Asset Store
1. Install the Asset Store Tools as usual: https://assetstore.unity.com/packages/tools/utilities/asset-store-tools-115
1. Open <kbd>Asset Store Tools/Package Upload</kbd>
1. Press <kbd>Select</kbd> and select a local or embedded package
2. Make sure "include dependencies" is off - you can now specify them through your package.json!
3. Press <kbd>Upload</kbd>

#### Multiple Packages
The AssetStoreTools only allow selecting one root folder.  
As a workaround, you can create an "Upload Config" that specifies multiple packages / root folders to upload.  

The basic flow is:  

1. Create a folder for your "package collection" in Assets, e.g. "My Package Collection".
2. In that folder, right click and create a `Needle/Asset Store Upload Config`.
3. Add entries to the `Selection`" array and drag the folders/files into the `Item` field. For packages, drag in the `package.json` since package folders can't be drag-dropped.
4. Open <kbd>Asset Store Tools/Package Upload</kbd>
1. Press <kbd>Select</kbd> and select the directory that contains your upload config, e.g. "My Package Collection"
3. Press <kbd>Upload</kbd>

## Known Issues / Limitations
- The optional <kbd>Validate</kbd> step isn't supported yet. Export your package via <kbd>Right Click/Export Package</kbd> and test in a separate project for now.
- Dependencies into other packages shouldn't be exported, so it's recommended to turn off "Include Dependencies" when exporting a package.
- If you run into any issues, you can temporarily disable the functionality via <kbd>Edit/Project Settings/Needle/Editor Patch Manager</kbd> or remove the package. Please report a bug!
- The `package.json` of your package can [define dependencies](https://docs.unity3d.com/Manual/upm-manifestPrj.html). However, only dependencies from the Unity Registry will be automatically resolved in empty projects - we'll need to think of a separate mechanism / guidance for people to add dependencies from scoped registries. For now, it's recommended that you guard against missing dependencies via [Version Defines](https://docs.unity3d.com/Manual/ScriptCompilationAssemblyDefinitionFiles.html#define-symbols).
- .unitypackage preview images for hidden folders will not be exported unless you're on 2020.1+ and had that folder in the AssetDatabase in the current session (e.g. the folder was named "Samples" and then "Samples~" to hide it before export).
- There's some support for .gitignore and .npmignore, but the behaviour isn't exactly the same as with npm/git (e.g. when multiple of these are in different folders, the order they are applied isn't always correct).
- The AssetStore Tools sometimes get stuck in an endless loop when trying to export nearly empty folders or empty multi-package sets. This seems to be an AssetStore Tools bug.

## Technical Details  

This is tested with Unity 2018.4, 2019.4, 2020.3 and Asset Store Tools 5.0.4.

All the functionality to use packages in .unitypackages is already provided by Unity. Just the tooling to create them has too many incorrect/outdated safety checks - so all we're doing here is bypassing those. It's still the same Unity APIs creating / exporting / importing packages.

## Ideas for Future Development
- Immutable Packages could be shipped in `.unitypackage` as well as `Packages/com.my.package.tgz`, but this needs a bit more work to create an additional archive and include that in the exported package.
- More safeguards around accidental inclusion of dependencies from other packages
- Ability to show hints/warnigs for people to ask them to manually install dependencies from scoped registries. The Unity Terms of Service currently prohibit doing this automatically.

## If you're Unity

We're happy to provide guidance on what we needed to change here. This took only a few hours to create, and ideally wouldn't be necessary - `AssetStoreTools.dll` and `.unitypackage` utilities in Unity could just work.

## Contact ✒️
<b>[🌵 needle — tools for unity](https://needle.tools)</b> • 
[@NeedleTools](https://twitter.com/NeedleTools) • 
[@marcel_wiessler](https://twitter.com/marcel_wiessler) • 
[@hybridherbst](https://twitter.com/hybridherbst) • 
[Needle Discord](https://discord.gg/CFZDp4b)