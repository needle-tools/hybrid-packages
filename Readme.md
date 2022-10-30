# Hybrid Packages

![Unity Version Compatibility](https://img.shields.io/badge/Unity-2018.4%20%E2%80%94%202022.1-brightgreen)

Unity has two separate package formats:
- `.unitypackage`, a zip-based file format that is used on the Asset Store
- `UPM package`, to prepare modules for Unity's package manager.
  
The latter is newer, enforces better code and directory structure (AsmDefs required), and generally much easier to work with / add / remove / update.

So far, it has been impossible to ship UPM packages on the Asset Store or via `.unitypackage` files.  
This package fixes that by introducing **Hybrid Packages**, regular .unitypackage files that contain the correct directory structures for UPM.  
After installing this package, you can select assets and folders from the Packages folder to be exported, which 
- allows packages to be uploaded to the Asset Store through the regular Asset Store Tools
- enables <kbd>Assets/Export Package</kbd> to export stuff from package folders directly

The resulting .unitypackage files _do not require any additional setup for users_. This package here is only required for creating those `.unitypackage` files, not for importing them.

**If you switch to Hybrid Packages today, the transition to "registry-based" packages will be much more painless, as all your code and content will be ready on day 1.**  

## Update (October 2022): Unity has listened and added Hybrid Packages support to their Asset Store Tools!  

It's experimental and needs to be explcitily enabled. There's two ways:  

- either add the Hybrid Packages package (this one)!  
- or add the scripting define `UNITY_ASTOOLS_EXPERIMENTAL` in Project Settings.  

> **Note**: We still recommend installing the Hybrid Packages package as it fixes some current issues in Unity's tool, like selecting packages with `file:..` references.   

This will unlock a new option in the Asset Store Uploader window:  

![20221030-024915_Unity](https://user-images.githubusercontent.com/2693840/198858071-9e3fc114-3636-4049-a2cf-d451f51297e9.png)

**Fun fact:** the Asset Store Tools ship as Hybrid Package themselves!  

![image](https://user-images.githubusercontent.com/2693840/198858153-57f5f9ba-c1c7-406e-9fb6-06594876a522.png)


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

   - open <kbd>Window/Package Manager</kbd>
   - click <kbd>+</kbd>
   - click <kbd>Add package from git URL</kbd> or <kbd>Add package by name</kbd>  
   - Add `https://github.com/needle-tools/upm-in-unitypackage.git` in Package Manager  

</details>

## How to use 💡

### Export a .unitypackage that contains files in Packages or entire packages
1. select what you want to export
2. hit <kbd>Assets/Export Package</kbd> (also available via right click on Assets/Folders).  

### Upload Packages to Asset Store directly

1. Install the Asset Store Tools as usual: https://assetstore.unity.com/packages/tools/utilities/asset-store-tools-115
2. Open <kbd>Asset Store Tools/Asset Store Uploader</kbd>
3. Find your draft package in the list (must have already created on Unity Publisher Portal)
4. Choose the "Local UPM Package" upload type
5. Use the "Browse" button to select your package directory
6. Select additional packages as needed in the "Extra Packages" section
7. Press <kbd>Upload</kbd>

#### Using an Upload Config to create and upload .unitypackage files

Using a configuration file makes it easier to specify settings for export, and allows for "Package Bundles" (multiple packages in one `.unitypackage`).  

Here's how it works:  

1. Create a folder for your "package collection" in Assets, e.g. "My Package Collection".
2. In that folder, right click and create a `Needle/Asset Store Upload Config`.
3. Add entries to the `Selection`" array and drag the folders/files into the `Item` field. 
   For packages, drag in the `package.json` since package folders can't be drag-dropped.  
   This can be a single file, a folder, one or multiple packages, or a combination of these.  
4. To test your Hybrid Package locally, select your config, and click <kbd>Export for Local Testing</kbd>.  
   This produces exactly the same .unitypackage as on Store upload, so you can test this one by importing it into a different/empty project.  
5. Open <kbd>Asset Store Tools/Asset Store Uploader</kbd>
4. Choose the "Pre-exported unitypackage" upload type
5. Select the .unitypackage you already exported that contains package data
7. Press <kbd>Upload</kbd>

## Known Issues / Limitations
- The optional <kbd>Validate</kbd> step isn't supported yet. Export your package via <kbd>Right Click/Export Package</kbd> or an Upload Config and test in a separate project for now.
- Dependencies into other packages shouldn't be exported, so you must turn off "Include Dependencies" when exporting a package.
- The `package.json` of your package can [define dependencies](https://docs.unity3d.com/Manual/upm-manifestPrj.html). However, only dependencies from the Unity Registry will be automatically resolved in empty projects - we'll need to think of a separate mechanism / guidance for people to add dependencies from scoped registries. For now, it's recommended that you guard against missing dependencies via [Version Defines](https://docs.unity3d.com/Manual/ScriptCompilationAssemblyDefinitionFiles.html#define-symbols).
- .unitypackage preview images for hidden folders will not be exported unless you're on 2020.1+ and had that folder in the AssetDatabase in the current session (e.g. the folder was named "Samples" and then "Samples~" to hide it before export).
- There's experimental support for .gitignore and .npmignore, but the behaviour isn't exactly the same as with npm/git (e.g. when multiple of these are in different folders, the order they are applied isn't always correct). You can turn this on on the UploadConfig asset.
- The AssetStore Tools sometimes get stuck in an endless loop when trying to export nearly empty folders or empty multi-package sets. This seems to be an AssetStore Tools bug.

----------

## FAQ / Why should I use this?

Hybrid Packages are an in-between solution. Unity is still not ready for a proper, registry-based package workflow for the Asset Store. Hybrid Packages allow Asset Store developers to switch to a package-based workflow today, with some (but not all) of the benefits of that, with no (known) downsides compared to the current workflow.  
If you switch to Hybrid Packages today, the transition to "registry-based" packages will be much more painless, as all your code and content will be ready on day 1.  

### How are these packaged up by your tool during submission?
Nothing changes, same process as the regular "Export > Unity Package" flow or "Asset Store Tools > Upload" (which are the same). Our tooling mostly just disables the check for "is this in assets?" and thus allows you to upload your embedded/local packages to the AssetStore.  

### When they are unpacked during installation, where do the assets wind up?
The files that before went into  
`Assets/YourContent/<all your content>`  
now end up in  
`Packages/com.your.content/ <all your content>`.   
This is what's called an embedded package. It is mutable, that is, users can change the files. It is not in the library, and files don't get duplicated into the `Library/PackageCache` folder. Also, this folder is not (and should not) be excluded from source control. From a file system perspective, it's really just a move from `Assets/` to `Packages/`, bringing some of the package benefits with it.  

Here's an example of how this looks for the Asset Store Tools themselves, which also ship as Hybrid Package:  
![image](https://user-images.githubusercontent.com/2693840/198858135-1795f67d-9121-4835-95e4-214c237ecfb7.png)

### How does Unity treat these assets when they are placed in the new (and non-standard) location?
For assets, nothing changes, no new/different rules. This is the same as if a user makes a `Packages/com.your.content/` folder and moves the files there (what a good number of users are already doing anyways). For scripts, Unity's rules for packages apply: code needs to be in AsmDefs to be compiled. This enforces better code structure, faster compilation, and is benefictial no matter if in "Assets" or "Packages".  

### Do these packages appear in the Package Manager at all? If so, can I use the "Install Sample" (or whatever it is) feature? How does that work?
Yes, this is one of the main points! Hybrid Packages
- show up in PackMan
- can use package.json dependencies
- can ship samples that can be installed via Package Manager. Samples are shipped with your .unitypackage in a folder called `Samples~` and set up via package.json. (see below for more on this)

### How does this differ from the package lifecycle for packages installed via Package Manager?
Local Packages don't come from a registry, they're just "local". That is, they can't be updated via `PackageManager/In Project`. They still get updated by users from `Package Manager/My Assets` by downloading the new version and it gets "unpacked" over the old, as before. So, same rules still apply for updates: if you removed files, ask users to delete the folder before importing.  

_Before_ the existance of Hybrid Packages, installing Asset Store Content in user projects was mostly a fire-and-forget operation:  
Users download a package, install it, and then neither the user nor Package Manager have any information that this content is actually in the project (of course the user can look for the folder, but the point is there's no real "entry point" for the content).  

_Now_, with Hybrid Packages, the content actually shows up in clear, structured places after installation:  
- as a package in the project, under `Packages/Your Content` in the Project Window  
- in the `In Project` section in Package Manager, with the ability to install Samples  
- Updates are still done in the `My Assets` section as before.  

When the package is _not_ installed, it only shows up in "My Assets", as before. No change here.  

### How do I prepare my asset for the package workflow?
You create a local or embedded package: you define your own package.json file and add the relevant details such as your package name/version/description/author, set up "Runtime" and "Editor" folders.  

You move your code and assets over: make sure that all scripts are part of AsmDef files, that are properly marked for "Runtime" or "Editor". Mostly, that's it!  

_Recommended:_ Add a documentationUrl in your package.json, pointing at your docs either locally or on the web. This will be opened when people click on "Documentation" for your package in Package Manager.  

Also see <a href="#guide-to-packages">Guide to Packages</a>.

### Now that dependencies can be specified via package.json, what happens to the "Include Dependencies" functionality in the Asset Store Tools?
The "Include Dependencies" is now redundant. It never worked great (sometimes including way too many dependencies), and you have much more fine-grained control by adding exactly the dependencies you need to your package.json.  
This has the additional benefit that dependencies aren't forcefully added to user project manifests (as with the Asset Store solution) - they are only resolved as long as your package is actually in the project.

### How do I add samples that can be installed via Package Manager?
In your package.json, you can declare an array of samples for your package:  

```"samples": [ { "displayName" : "..", "description":"...", "path":"Samples~/..." } ]```  

Samples are stored in folders. These all live in a folder called `Samples~` (note the tilde), which is hidden in Unity.  
While you work on your samples, you simply rename the folder to `Samples`, and before submitting, you rename it back to `Samples~`.  

The advantage of shipping samples this way is that while samples are already downloaded and ready to be used, they don't clutter the asset database of user projects – only once people import the sample is it copied over to their Assets folders, ready to be adjusted as needed.  

### What differences, if any, are there regarding Unity's Special Folders? Is the functionality the same? Do Gizmos and other folders still work as expected?

### Future: How do I prepare my asset for the future "registry-based" package workflow?

_Note: some of the below depends on how Unity implements this for Asset Store publishers._

The main change with "registry-based" packages is that they are immutable, which means that users can't change the files in the package (they can still "embed" the package and make it locally editable, like you can for all Unity packages).  

But the goal is that your package is a single thing, nobody changes the content of it. Users then compose their project out of these assets as usual, e.g. they use animation files, models, materials, make new prefab variants, use components, ...  

One thing that you can't do anymore at this point is write your own files (e.g. settings) into your package once it's in users hands. Thus, your settings should live in a proper place depending on whether they need to be accessible in the Editor only (ProjectSettings/UserSettings) or at runtime (Assets/Resources).  
If your code reads/writes from a settings file, this is a good point in time to modernize this, if you haven't already. Make sure settings are accessible via a  SettingsProvider](https://docs.unity3d.com/ScriptReference/SettingsProvider.html?), and are either stored in "ProjectSettings", "UserSettings" or "Assets/Resources" (for settings that need to be available at runtime).  

## Technical Details  

This is tested with Unity 2018.4, 2019.4, 2020.3 and Asset Store Tools 5.0.4.

All the functionality to use packages in .unitypackages is already provided by Unity. Just the tooling to create them has too many incorrect/outdated safety checks - so all we're doing here is bypassing those. It's still the same Unity APIs creating / exporting / importing packages.

## Ideas for Future Development
- Immutable Packages could be shipped in `.unitypackage` as well as `Packages/com.my.package.tgz`, but this needs a bit more work to create an additional archive and include that in the exported package.
- ~~More safeguards around accidental inclusion of dependencies from other packages~~
- Ability to show hints/warnigs for people to ask them to manually install dependencies from scoped registries. The Unity Terms of Service currently prohibit doing this automatically.

## If you're Unity

We're happy to provide guidance on what we needed to change here. This took only a few hours to create, and ideally wouldn't be necessary - `AssetStoreTools.dll` and `.unitypackage` utilities in Unity could just work.

## Guide to Packages


**Option 1: Single-Project Workflow**
A single project where you work on your asset.  

```
Your Test Project
├── Assets
└── Packages
    ├── manifest.json
    ├── manifest-lock.json
    └── com.your.package "Your package root folder"
        ├── package.json "Specify package name, version, author, dependencies, samples, documentationUrl here"
        ├── Changelog.md "This should follow SemVer conventions. See sample"
        ├── Readme.md    "Some basic info to get started"
        ├── Runtime
        │   ├── Your.Package.asmdef
        │   └── "your runtime code and assets"
        ├── Editor
        │   │── Your.Package.Editor.asmdef "This AsmDef must be set to Editor-only"
        │   └── "your editor code and assets"
        └── Samples~ "This folder is hidden, samples get specified in package.json"
            ├── Simple Sample
            │   └── "your sample code and files"
            └── Complex Sample
                └── "your sample code and files"
```

**Option 2: Multi-Project Workflow (recommended)**   
Many packages need to support multiple Unity versions. This has traditionally been pretty cumbersome to maintain, with multiple submodules in different projects, copy-pasting code over, or just hoping everything works.  
One of the biggest advantages of a package-based workflow is that cross-version development becomes incredibly easy, as the same package on disk can be referenced by multiple projects. Testing code is as simple as focussing the right Unity Editor instance.    

```
Your Content Development Folder "often the git repository root"
├── Your Test Project on Unity 2019.4
│   ├── Assets
│   └── Packages
│       ├── manifest.json "contains the line: " "com.your.package":"file:../../com.your.package"
│       └── manifest-lock.json
│
├── Your Test Project on Unity 2020.3
│   ├── Assets
│   └── Packages
│       ├── manifest.json "contains the line: " "com.your.package":"file:../../com.your.package"
│       └── manifest-lock.json
│
├── Your Test Project on Unity 2021.3
│   ├── Assets
│   └── Packages
│       ├── manifest.json "contains the line: " "com.your.package":"file:../../com.your.package"
│       └── manifest-lock.json
│
└── com.your.package "Your package root folder"
        ├── package.json "Specify package name, version, author, dependencies, samples, documentationUrl here"
        ├── Changelog.md "This should follow SemVer conventions. See sample"
        ├── Readme.md    "Some basic info to get started"
        ├── Runtime
        │   ├── Your.Package.asmdef
        │   └── "your runtime code and assets"
        ├── Editor
        │   │── Your.Package.Editor.asmdef "This AsmDef must be set to Editor-only"
        │   └── "your editor code and assets"
        └── Samples~ "This folder is hidden, samples get specified in package.json"
            ├── Simple Sample
            │   └── "your sample code and files"
            └── Complex Sample
                └── "your sample code and files"
   
```

**Complete Example: `package.json`**

```json
{
   "name": "com.needle.my-package",
   "version": "2.3.0",
   "displayName": "My Package",
   "description": "Shows how to set up a Package",
   "author":
   {
      "name": "Needle",
      "email": "hi@needle.tools",
      "url": "https://needle.tools"
   },
   "unity": "2019.4",
   "type": "tool",
   "documentationUrl": "https://github.com/needle-tools/upm-in-unitypackage#readme",
   "samples":
   [
      {
         "displayName": "Simple Sample",
         "description": "A very simple example, enough to get started.",
         "path": "Samples~/Simple Sample"
      },
      {
         "displayName": "Complex Sample",
         "description": "Shows everything this package can do.",
         "path": "Samples~/Complex Sample"
      }
   ]
}
```

**Complete Example: `Changelog.md`**

```md
# Changelog
All notable changes to this package will be documented in this file.
The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/) and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2021-02-12
- added: new horse
- fixed: old horse sometimes lost teeth

## [1.1.0] - 2021-01-08
- added: horse controller and sample
- fixed: sample didn't actually explain how everything works

## [1.0.0] - 2021-01-05
- initial release with one horse
```

**Complete Example: Single-Project Folder Structure with settings**

```
Your Test Project
├── Assets
│   └── Resources
│       └── YourPackageRuntimeSettings.asset "Your package should generate this if needed. Note, Users might move it somewhere else!"
├── Packages
│   ├── manifest.json
│   ├── manifest-lock.json
│   └── com.your.package "Your package root folder"
│       ├── package.json "Specify package name, version, author, dependencies, samples, documentationUrl here"
│       ├── Changelog.md "This should follow SemVer conventions. See sample"
│       ├── Readme.md    "Some basic info to get started"
│       ├── Runtime
│       │   ├── Your.Package.asmdef
│       │   └── "your runtime code and assets"
│       ├── Editor
│       │   │── Your.Package.Editor.asmdef "This AsmDef must be set to Editor-only"
│       │   └── "your editor code and assets"
│       └── Samples~ "This folder is hidden, samples get specified in package.json"
│           ├── Simple Sample
│           │   └── "your sample code and files"
│           └── Complex Sample
│               └── "your sample code and files"
├── ProjectSettings
│   └── YourPackageProjectSettings.asset "If your package has project-specific settings, put them here"
└── UserSettings
    └── YourPackageUserSettings.asset "If your package has user-specific settings, put them here"
```

**Complete Example: Settings Provider**  
```TODO // Move somewhere else```

## Contact ✒️
<b>[🌵 needle — tools for unity](https://needle.tools)</b> • 
[@NeedleTools](https://twitter.com/NeedleTools) • 
[@marcel_wiessler](https://twitter.com/marcel_wiessler) • 
[@hybridherbst](https://twitter.com/hybridherbst) • 
[Needle Discord](https://discord.gg/CFZDp4b)
