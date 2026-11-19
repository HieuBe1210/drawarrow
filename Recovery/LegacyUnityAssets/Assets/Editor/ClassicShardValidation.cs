using System;
using UnityEditor;
using UnityEngine;

namespace DrawArrow.Editor
{
    public static class ClassicShardValidation
    {
        [MenuItem("Draw Arrow/Validate Level Set 1 source")]
        public static void Run()
        {
            var repository = new LevelRepository();
            if (repository.PlayableClassicCount != 22583)
                throw new Exception("Expected the intro and all 46 shards.");

            for (int i = 0; i < 40; i++)
                if (JsonUtility.ToJson(repository.LoadPlayable(GameMode.Classic, i)) !=
                    JsonUtility.ToJson(repository.Load(GameMode.Classic, i)))
                    throw new Exception("Intro changed at level " + (i + 1));

            CheckShardLevel(41, "00002c66e9823004");
            CheckShardLevel(42, "00006ffab0242f29");
            CheckShardLevel(540, "09469a0943877978");
            CheckShardLevel(541, "094a702973cbba03");
            CheckShardLevel(22583, "sprite-9e0fd51e5363708e");
            CheckShardLevel(repository.PlayableClassicCount + 41, "00002c66e9823004");
            Debug.Log("CLASSIC_SHARDS_VALIDATED levels=" + repository.PlayableClassicCount);

            void CheckShardLevel(int number, string id)
            {
                int index = number - 1;
                if (repository.LoadPlayable(GameMode.Classic, index).id != id ||
                    !repository.UsesOriginalClassicLayout(GameMode.Classic, index))
                    throw new Exception("Unexpected source or layout at level " + number);
            }
        }
    }
}
