using System;
using UnityEditor;
using UnityEngine;

namespace IdleBattle.Editor
{
    /// <summary>AdMob 공식 테스트 앱 ID가 비어 있을 때만 자동으로 채웁니다.</summary>
    [InitializeOnLoad]
    internal static class AdMobTestSettingsSetup
    {
        static AdMobTestSettingsSetup()
        {
            const string folder = "Assets/GoogleMobileAds/Resources";
            const string path = folder + "/GoogleMobileAdsSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (settings == null)
            {
                var type = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
                if (type == null) return;
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/GoogleMobileAds", "Resources");
                settings = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(settings, path);
            }
            var serialized = new SerializedObject(settings);
            var android = serialized.FindProperty("adMobAndroidAppId");
            var ios = serialized.FindProperty("adMobIOSAppId");
            var changed = false;
            if (android != null && string.IsNullOrWhiteSpace(android.stringValue))
            {
                android.stringValue = "ca-app-pub-3940256099942544~3347511713";
                changed = true;
            }
            if (ios != null && string.IsNullOrWhiteSpace(ios.stringValue))
            {
                ios.stringValue = "ca-app-pub-3940256099942544~1458002511";
                changed = true;
            }
            if (!changed) return;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
