using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace IdleBattle.EditorTools
{
    /// <summary>
    /// Google Sign-In Unity Plugin이 프로젝트에 임포트되어 있는지 확인해서
    /// GOOGLE_SIGNIN_PRESENT 스크립팅 심볼을 자동으로 켜고 끈다.
    /// 덕분에 플러그인이 없는 상태에서도 프로젝트가 그대로 컴파일된다.
    /// </summary>
    [InitializeOnLoad]
    internal static class GoogleSignInDefineSetup
    {
        private const string Symbol = "GOOGLE_SIGNIN_PRESENT";
        private const string PluginTypeName = "Google.GoogleSignIn";

        private static readonly NamedBuildTarget[] Targets =
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS
        };

        static GoogleSignInDefineSetup()
        {
            // 임포트 직후에는 어셈블리가 아직 안 올라와 있을 수 있어서 한 틱 미룬다.
            EditorApplication.delayCall += Sync;
        }

        /// <summary>메뉴로 직접 눌렀을 때는 바뀐 게 없더라도 현재 상태를 알려준다.</summary>
        [MenuItem("Tools/Firebase/Google Sign-In 심볼 다시 확인")]
        private static void SyncFromMenu()
        {
            Sync();

            var pluginPresent = IsPluginPresent();
            var current = string.Join(", ", Targets.Select(DescribeTarget).Where(text => text != null));
            EditorUtility.DisplayDialog(
                "Google Sign-In",
                pluginPresent
                    ? $"플러그인을 찾았습니다. {Symbol} 심볼이 켜져 있습니다.\n\n{current}"
                    : $"플러그인이 없습니다. {Symbol} 심볼을 껐습니다.\n\n{current}",
                "확인");
        }

        private static string DescribeTarget(NamedBuildTarget target)
        {
            try
            {
                var defines = PlayerSettings.GetScriptingDefineSymbols(target);
                var hasSymbol = defines
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(symbol => symbol.Trim() == Symbol);
                return $"{target.TargetName}: {(hasSymbol ? "ON" : "OFF")}";
            }
            catch (Exception)
            {
                // 해당 플랫폼 모듈이 설치되어 있지 않은 경우
                return null;
            }
        }

        private static void Sync()
        {
            var pluginPresent = IsPluginPresent();

            foreach (var target in Targets)
            {
                string defines;
                try
                {
                    defines = PlayerSettings.GetScriptingDefineSymbols(target);
                }
                catch (Exception)
                {
                    // 해당 플랫폼 모듈이 설치되어 있지 않은 경우
                    continue;
                }

                var symbols = defines
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(symbol => symbol.Trim())
                    .Where(symbol => symbol.Length > 0)
                    .ToList();

                var hasSymbol = symbols.Contains(Symbol);
                if (hasSymbol == pluginPresent) continue;

                if (pluginPresent) symbols.Add(Symbol);
                else symbols.RemoveAll(symbol => symbol == Symbol);

                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
                Debug.Log($"[GoogleSignIn] {target.TargetName}: {Symbol} 심볼을 {(pluginPresent ? "추가" : "제거")}했습니다.");
            }
        }

        private static bool IsPluginPresent()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetType(PluginTypeName, false) != null) return true;
                }
                catch (Exception)
                {
                    // 로드 실패한 어셈블리는 건너뛴다.
                }
            }

            return false;
        }
    }
}
