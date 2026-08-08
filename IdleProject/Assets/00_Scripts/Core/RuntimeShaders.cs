using UnityEngine;

namespace IdleBattle
{
    /// <summary>
    /// 런타임 생성 머티리얼이 쓰는 셰이더를 한 번만 조회해 캐싱합니다.
    /// Shader.Find는 이름으로 전체 셰이더 목록을 훑기 때문에 머티리얼을 만들 때마다 호출하면 안 됩니다.
    /// </summary>
    public static class RuntimeShaders
    {
        private static Shader lit;

        /// <summary>URP Lit 셰이더. URP가 없는 프로젝트에서는 Standard로 대체합니다.</summary>
        public static Shader Lit
        {
            get
            {
                if (lit != null) return lit;
                lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                return lit;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            // Domain Reload를 끈 상태에서 이전 플레이의 참조가 남지 않게 합니다.
            lit = null;
        }
    }
}
