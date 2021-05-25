using UnityEditor;

namespace Needle.MyPackage
{
    [CustomEditor(typeof(MySampleComponent))]
    public class MySampleComponentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("My Sample Component", EditorStyles.boldLabel);
            DrawDefaultInspector();
        }
    }
}