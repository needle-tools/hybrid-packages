using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Needle.PackageTools
{
    [CreateAssetMenu(menuName = "Needle/Asset Store Upload Config")]
    public class AssetStoreUploadConfig : ScriptableObject
    {
        public List<Object> folders;
        
        public bool IsValid => folders != null && folders.Any();

        [MenuItem("Needle/Show Data Type for Selected")]
        static void ShowDataType()
        {
            Debug.Log(string.Join("\n", Selection.objects.Select(x => x + " " + x.GetType())));
        }

        public string[] GetExportPaths()
        {
            HashSet<string> exportPaths = new HashSet<string>();
            
            foreach (var folder in folders)
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
                
                // sanitize: do not allow uploading packages that are in the Library
                var libraryRoot = Path.GetFullPath(Application.dataPath + "/../Library");
                if (Path.GetFullPath(path).StartsWith(libraryRoot, StringComparison.Ordinal))
                    return null;
                
                // sanitize: do not allow re-uploading of Unity-scoped packages
                if (packageName.StartsWith("com.unity.", StringComparison.OrdinalIgnoreCase))
                    return null;
                
                return AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            }

            return obj;
        }
    }

    [CustomEditor(typeof(AssetStoreUploadConfig))]
    public class AssetStoreUploadConfigEditor : Editor
    {
        private ReorderableList itemList;
        
        private void OnEnable()
        {
            var t = target as AssetStoreUploadConfig;
            itemList = new ReorderableList(serializedObject, serializedObject.FindProperty("folders"), true, false, true, true);
            itemList.elementHeight = 60;
            itemList.drawElementCallback += (rect, index, active, focused) =>
            {
                var selectedObject = itemList.serializedProperty.GetArrayElementAtIndex(index);
                rect.height = 20;
                EditorGUI.PropertyField(rect, selectedObject, new GUIContent("File"));
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
                    EditorGUI.LabelField(rect, "Will be exported directly", EditorStyles.miniLabel);
                }
            };
        }

        public override void OnInspectorGUI()
        {
            var t = target as AssetStoreUploadConfig;
            EditorGUILayout.LabelField(new GUIContent("Selection", "Select all root folders and assets that should be exported. For packages, select the package.json."), EditorStyles.boldLabel);
            itemList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
            EditorGUI.BeginDisabled(true);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Export Roots", "All content from these folders will be included when exporting with this configuration."), EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            var paths = t.GetExportPaths();
            foreach (var p in paths)
            {
                EditorGUILayout.LabelField(p);
            }
            EditorGUI.indentLevel--;
            EditorGUI.EndDisabled();
        }
    }
}