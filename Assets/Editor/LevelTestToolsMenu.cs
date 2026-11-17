using UnityEditor;
using UnityEngine;

namespace DrawArrow.Editor
{
    public static class LevelTestToolsMenu
    {
        const string Menu="Draw Arrow/Test Tools/Enable Level Tools";
        [MenuItem(Menu)]
        static void Toggle()
        {
            var settings=Resources.Load<LevelTestToolsSettings>("LevelTestToolsSettings");
            if(!settings)return;
            Undo.RecordObject(settings,"Toggle level test tools");
            settings.enableLevelTools=!settings.enableLevelTools;
            EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
        }
        [MenuItem(Menu,true)]
        static bool ValidateToggle()
        {
            var settings=Resources.Load<LevelTestToolsSettings>("LevelTestToolsSettings");
            UnityEditor.Menu.SetChecked(Menu,settings&&settings.enableLevelTools);return settings;
        }
    }
}
