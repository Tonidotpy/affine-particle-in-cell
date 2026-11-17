using UnityEditor;
public class ForceSync
{
    [MenuItem("Tools/Force Generate Project Files")]
    public static void Sync()
    {
        Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        UnityEngine.Debug.Log("Project files generated!");
    }
}
