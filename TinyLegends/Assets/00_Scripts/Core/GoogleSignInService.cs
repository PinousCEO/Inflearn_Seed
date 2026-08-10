using System;
using System.Threading.Tasks;
using UnityEngine;
#if GOOGLE_SIGNIN_PRESENT
using Google;
#endif

namespace IdleBattle
{
    /// <summary>
    /// Google Sign-In Unity Plugin을 감싸서 Firebase가 필요로 하는 ID 토큰만 돌려준다.
    /// 플러그인이 아직 임포트되지 않았으면 GOOGLE_SIGNIN_PRESENT 심볼이 꺼지고
    /// IsAvailable이 false가 되므로, 프로젝트는 플러그인 없이도 컴파일된다.
    /// (심볼은 Editor/GoogleSignInDefineSetup이 자동으로 켜고 끈다.)
    /// </summary>
    public static class GoogleSignInService
    {
        /// <summary>
        /// google-services.json의 oauth_client 중 client_type: 3(웹 클라이언트) ID.
        /// 안드로이드 네이티브 로그인이 ID 토큰을 발급받을 때 이 값을 서버 클라이언트로 사용한다.
        /// Firebase 콘솔에서 프로젝트를 바꾸면 이 상수도 같이 갱신할 것.
        /// </summary>
        public const string WebClientId = "671338895740-l7taa5r83adqeqvs6ak0ej4lp4cdr39p.apps.googleusercontent.com";

        public const string PluginMissingMessage =
            "Google Sign-In Unity Plugin이 프로젝트에 없습니다. " +
            "google-signin-unity(.unitypackage)를 임포트한 뒤 다시 시도하세요.";

#if GOOGLE_SIGNIN_PRESENT
        public static bool IsAvailable => true;

        private static bool isConfigured;

        /// <summary>구글 계정을 선택받아 Firebase 인증에 넘길 ID 토큰을 반환한다.</summary>
        public static async Task<string> GetIdTokenAsync()
        {
            Configure();

            GoogleSignInUser user;
            try
            {
                user = await GoogleSignIn.DefaultInstance.SignIn();
            }
            catch (Exception exception)
            {
                // 플러그인이 태스크를 AggregateException으로 감싸는 경로도 있어서 한 번 벗겨준다.
                throw Describe((exception as AggregateException)?.InnerException ?? exception);
            }

            if (user == null || string.IsNullOrEmpty(user.IdToken))
                throw new InvalidOperationException(
                    "Google 로그인은 끝났지만 ID 토큰이 비어 있습니다. " +
                    "Firebase 콘솔의 웹 클라이언트 ID와 GoogleSignInService.WebClientId가 같은지 확인하세요.");

            Debug.Log($"Google sign-in succeeded. Email: {user.Email}");
            return user.IdToken;
        }

        /// <summary>다음 로그인 때 계정 선택 창이 다시 뜨도록 구글 세션을 끊는다.</summary>
        public static void SignOut()
        {
            if (!isConfigured) return;

            GoogleSignIn.DefaultInstance.SignOut();
            // 플러그인이 SignOut에서 Configuration을 비우므로 다음 로그인 때 다시 설정해야 한다.
            isConfigured = false;
        }

        private static void Configure()
        {
            if (isConfigured) return;

            GoogleSignIn.Configuration = new GoogleSignInConfiguration
            {
                WebClientId = WebClientId,
                RequestIdToken = true,
                RequestEmail = true,
                UseGameSignIn = false
            };
            isConfigured = true;
        }

        private static Exception Describe(Exception exception)
        {
            if (exception is GoogleSignIn.SignInException signInException)
            {
                // DeveloperError(10)는 대부분 SHA-1 지문 또는 패키지명 불일치다.
                return new InvalidOperationException(
                    $"Google 로그인 실패({signInException.Status}). " +
                    "Firebase 콘솔에 이 빌드의 SHA-1 지문이 등록되어 있는지, " +
                    "패키지명이 google-services.json과 일치하는지 확인하세요.",
                    exception);
            }

            return exception;
        }
#else
        public static bool IsAvailable => false;

        public static Task<string> GetIdTokenAsync() =>
            Task.FromException<string>(new InvalidOperationException(PluginMissingMessage));

        public static void SignOut()
        {
        }
#endif
    }
}
