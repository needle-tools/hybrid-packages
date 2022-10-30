using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Needle.HybridPackages
{
    [CreateAssetMenu(menuName = "Needle/Asset Store Upload Config")]
    public class AssetStoreUploadConfig : ScriptableObject  
    {
        public List<Object> items;
        public Zipper.CompressionStrength compressionStrength = Zipper.CompressionStrength.Normal;
        public bool respectIgnoreFiles = false;
        
        public bool IsValid => items != null && items.Any(x => GetActualExportObject(x));

        public string[] GetExportPaths()
        {
            if (items == null || !items.Any()) return new string[] { };
            
            HashSet<string> exportPaths = new HashSet<string>();
            foreach (var folder in items)
            {
                var actualExport = GetActualExportObject(folder);
                if(actualExport)
                    exportPaths.Add(AssetDatabase.GetAssetPath(actualExport));
            }

            return exportPaths.ToArray();
        }
        
        public Object GetActualExportObject(Object obj)
        {
            if (!obj) return null;
            
            var path = AssetDatabase.GetAssetPath(obj);
            
            if(path.StartsWith("Packages/", StringComparison.Ordinal))
            {
                path = path.Substring("Packages/".Length);
                var indexOfSlash = path.IndexOf("/", StringComparison.Ordinal);
                var packageName = path.Substring(0, indexOfSlash);
                path = "Packages/" + packageName;
                
                if(!Unsupported.IsDeveloperMode())
                {
                    // sanitize: do not allow uploading packages that are in the Library
                    var libraryRoot = Path.GetFullPath(Application.dataPath + "/../Library");
                    if (Path.GetFullPath(path).StartsWith(libraryRoot, StringComparison.Ordinal))
                        return null;
                    
                    // sanitize: do not allow re-uploading of Unity-scoped packages
                    if (packageName.StartsWith("com.unity.", StringComparison.OrdinalIgnoreCase))
                        return null;
                }
                return AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            }

            return obj;
        }
        
        public string GetExportFilename(string outputSubFolder)
        {
            if (!this) return null;
            return outputSubFolder+"/HybridPackage_" + Path.GetDirectoryName(AssetDatabase.GetAssetPath(this))
                       .Replace("\\", "/")
                       .Replace("Assets/", "")
                       .Replace("Packages/", "")
                       .Replace("/", "_")
                       .Trim('_')
                   + ".unitypackage";
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(AssetStoreUploadConfig))]
    internal class AssetStoreUploadConfigEditor : Editor
    {
        private ReorderableList itemList;
        
        private void OnEnable()
        {
            var t = target as AssetStoreUploadConfig;
            if (!t) return;
            
            itemList = new ReorderableList(serializedObject, serializedObject.FindProperty(nameof(AssetStoreUploadConfig.items)), true, false, true, true);
            itemList.elementHeight = 60;
            itemList.drawElementCallback += (rect, index, active, focused) =>
            {
                var selectedObject = itemList.serializedProperty.GetArrayElementAtIndex(index);
                rect.height = 20;
                EditorGUI.PropertyField(rect, selectedObject, new GUIContent("Item"));
                rect.y += 20;
                var actuallyExportedObject = t.GetActualExportObject(itemList.serializedProperty.GetArrayElementAtIndex(index).objectReferenceValue);
                if(selectedObject.objectReferenceValue != actuallyExportedObject)
                {
                    if (!actuallyExportedObject)
                    {
                        EditorGUI.HelpBox(rect, "This file/package can't be exported.", MessageType.Error);
                    }
                    else
                    {
                        EditorGUI.ObjectField(rect, "Exported", actuallyExportedObject, typeof(Object), false);
                        rect.y += 20;
                        EditorGUI.LabelField(rect, "The entire Package will be exported.", EditorStyles.miniLabel);
                    }
                }
                else
                {
                    EditorGUI.HelpBox(rect, "This file is not part of a package.", MessageType.Warning);
                    rect.y += 20;
                    EditorGUI.LabelField(rect, "Please export a regular .unitypackage.", EditorStyles.miniLabel);
                }
            };
        }

        private static readonly GUIContent RespectIgnoreFilesContent = new GUIContent("Respect Ignore Files (experimental)", "Uses .gitignore and .npmignore to filter which files should be part of the package.");

        private static GUIStyle wordWrapLabel;
        
        public override void OnInspectorGUI()
        {
            var t = target as AssetStoreUploadConfig;
            if (!t) return;
            
            if(targets.Length <= 1)
            {
                EditorGUILayout.LabelField(new GUIContent("Selection", "Select all root folders and assets that should be exported. For packages, select the package.json."), EditorStyles.boldLabel);
                itemList.DoLayoutList();
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(t.respectIgnoreFiles)), RespectIgnoreFilesContent);
                serializedObject.ApplyModifiedProperties();
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Export to folder:"), EditorStyles.boldLabel);
            
            EditorGUI.indentLevel++;
            if (wordWrapLabel == null) wordWrapLabel = new GUIStyle( GUI.skin.GetStyle( "label" )){ wordWrap = true };
            var outputLocation = Path.GetDirectoryName( Application.dataPath ) + "/Temp";
            EditorGUILayout.LabelField(new GUIContent(outputLocation), wordWrapLabel);
            
            if (GUILayout.Button("Export for Local Testing" + (targets.Length > 1 ? " [" + targets.Length + "]" : "")))
            {
                foreach (var o in targets)
                {
                    var config = (AssetStoreUploadConfig) o;
                    if(!config) continue;
                    AssetStoreToolsPatchProvider.ExportPackageForConfig(config);
                }
            }
            
            if( GUILayout.Button( "Open Export Folder" ) )
            {
                Application.OpenURL( outputLocation );
            }
            
            #if HAVE_AS_TOOLS_PACKAGE
            if (GUILayout.Button("Open Asset Store Uploader"))
            {
                EditorApplication.ExecuteMenuItem("Asset Store Tools/Asset Store Uploader");
            }
            #endif
            
            EditorGUI.indentLevel--;
            

            EditorGUILayout.Space();
            EditorGUI.BeginDisabled(true);
            EditorGUILayout.Space();
            foreach(var o in targets)
            {
                var c = (AssetStoreUploadConfig) o;
                EditorGUILayout.LabelField(new GUIContent("Export Roots for " + c.name, "All content from these folders will be included when exporting with this configuration."), EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                var paths = c.GetExportPaths();
                foreach (var p in paths)
                {
                    EditorGUILayout.LabelField(p);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            EditorGUI.EndDisabled();
        }
    }
}