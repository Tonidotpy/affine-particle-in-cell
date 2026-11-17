using UnityEditor;
using UnityEngine;

public class ForceEditorChange
{
    [MenuItem("Tools/Set Neovim as Code")]
    public static void SetEditor()
    {
        // Gets your absolute path: /home/nicolo/.local/bin/code
        string home = System.Environment.GetEnvironmentVariable("HOME");
        string fakeCodePath = $"{home}/.local/bin/code";

        // Bypasses the broken UI to force the preference change
        Unity.CodeEditor.CodeEditor.SetExternalScriptEditor(fakeCodePath);
        
        // Forces the C# project files to generate
        Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        
        Debug.Log($"Editor successfully forced to {fakeCodePath}!");
    }
}
