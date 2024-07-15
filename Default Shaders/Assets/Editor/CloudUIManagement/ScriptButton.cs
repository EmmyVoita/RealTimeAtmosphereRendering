using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShapeSettings))]
public class ScriptButton : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var script = (ShapeSettings)target;

        if (GUILayout.Button("Reset To Default Values", GUILayout.Height(40)))
        {
            script.Reset();
        }

    }
}