using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LightingSettings))]
public class ScriptButtonLighting : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var script = (LightingSettings)target;

        if (GUILayout.Button("Reset To Default Values", GUILayout.Height(40)))
        {
            script.Reset();
        }

    }
}