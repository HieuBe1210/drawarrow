using UnityEngine;

namespace DrawArrow
{
    [CreateAssetMenu(menuName="Draw Arrow/Level Test Tools Settings")]
    public sealed class LevelTestToolsSettings : ScriptableObject
    {
        [Tooltip("Show level reset/jump tools in the Editor and Development Builds. Store/release builds always exclude these tools.")]
        public bool enableLevelTools=true;
        static LevelTestToolsSettings settings;
        public static bool Enabled
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(!settings)settings=Resources.Load<LevelTestToolsSettings>("LevelTestToolsSettings");
                return settings&&settings.enableLevelTools;
#else
                return false;
#endif
            }
        }
    }
}
