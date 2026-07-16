using UnityEngine;

namespace IdleBattle
{
    // Animation Event는 Animator가 붙은 GameObject에서 메서드를 찾으므로 중계합니다.
    public sealed class AttackAnimationEventRelay : MonoBehaviour
    {
        [System.NonSerialized] public IdleBattleGame owner;

        public void ATK()
        {
            if (owner != null) owner.ATK();
        }
    }
}
