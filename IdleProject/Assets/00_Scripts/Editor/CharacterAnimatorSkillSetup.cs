using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IdleBattle.Editor
{
    public static class CharacterAnimatorSkillSetup
    {
        private const string ControllerPath = "Assets/02_Animations/00_Animator/Character.controller";
        private const string AnimationFolder = "Assets/02_Animations/01_Animation/00_Characters";
        private const string AutoSetupSessionKey = "IdleBattle.CharacterAnimatorSkillSetup.Completed";

        private const string IdleClipPath = "Assets/02_Animations/01_Animation/Idle.fbx";
        private const string RunClipPath = AnimationFolder + "/Run.fbx";

        /// <summary>
        /// 배열 순서가 곧 Num 값이자 <see cref="IdleBattle.SkillData"/>의 AnimationIndex입니다.
        /// 1-5만 다른 폴더에 들어와 있어 경로를 통째로 적습니다.
        /// </summary>
        private static readonly string[] SkillClipPaths =
        {
            AnimationFolder + "/1-1.fbx",
            AnimationFolder + "/1-2.fbx",
            AnimationFolder + "/1-3.fbx",
            AnimationFolder + "/1-4.fbx",
            "Assets/01_Prefabs/Skills/1-5.fbx"
        };

        /// <summary>
        /// 상태 이름 → 그 상태가 재생해야 할 클립의 에셋 경로입니다.
        ///
        /// .controller 파일은 클립을 `fileID`라는 번호로 붙잡고 있습니다.
        /// 그런데 FBX를 다시 임포트하면 이 번호가 바뀌는 일이 있고, 그러면 참조가 끊겨
        /// **모션이 없는 빈 상태**가 됩니다. Write Defaults가 켜져 있으므로 빈 상태는
        /// 모든 뼈를 기본 자세로 되돌립니다. 그게 화면에 보이는 T-Pose입니다.
        ///
        /// 실제로 CombatIdle이 이렇게 끊겨 있었고, 스킬을 쓰지 않는 내내 T-Pose였습니다.
        /// 번호가 아니라 **경로**로 다시 찾아 붙이면 같은 사고가 반복되지 않습니다.
        /// </summary>
        private static string GetClipPathForState(string stateName)
        {
            if (stateName == "CombatIdle") return IdleClipPath;
            if (stateName == "Run") return RunClipPath;

            for (var i = 0; i < SkillClipPaths.Length; i++)
                if (stateName == $"Skill{i + 1}")
                    return SkillClipPaths[i];

            return null;
        }

        [InitializeOnLoadMethod]
        private static void ConfigureOnceAfterCompile()
        {
            if (SessionState.GetBool(AutoSetupSessionKey, false))
                return;

            SessionState.SetBool(AutoSetupSessionKey, true);
            EditorApplication.delayCall += () =>
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                if (controller == null)
                    return;

                var alreadyConfigured = controller.parameters.Any(x =>
                                            x.name == "Num" &&
                                            x.type == AnimatorControllerParameterType.Int) &&
                                        controller.layers[0].stateMachine.stateMachines.Any(x =>
                                            x.stateMachine.name == "Skills" &&
                                            x.stateMachine.states.Length == SkillClipPaths.Length);
                if (!alreadyConfigured)
                {
                    Configure();
                    return;
                }

                // 구조가 멀쩡해도 클립 참조만 끊어질 수 있으므로 매번 확인합니다.
                RepairMissingMotions(controller);
            };
        }

        /// <summary>
        /// 모션이 비어 있는 상태를 경로로 다시 찾아 붙입니다.
        /// 고치지 못하면 조용히 T-Pose가 되게 두지 않고 에러로 알립니다.
        /// </summary>
        public static bool RepairMissingMotions(AnimatorController controller)
        {
            var repaired = false;

            foreach (var state in EnumerateStates(controller))
            {
                if (state.motion != null)
                    continue;

                var path = GetClipPathForState(state.name);
                var clip = path != null ? LoadMainAnimationClip(path) : null;
                if (clip == null)
                {
                    Debug.LogError(
                        $"Animator 상태 '{state.name}'에 모션이 없습니다. " +
                        $"이대로 두면 그 상태에 있는 동안 T-Pose가 보입니다. (기대한 클립: {path ?? "알 수 없음"})");
                    continue;
                }

                state.motion = clip;
                repaired = true;
                Debug.Log($"Animator 상태 '{state.name}'의 끊어진 모션을 {path}로 다시 연결했습니다.");
            }

            if (repaired)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
            }

            return repaired;
        }

        private static System.Collections.Generic.IEnumerable<AnimatorState> EnumerateStates(
            AnimatorController controller)
        {
            foreach (var layer in controller.layers)
            {
                foreach (var child in layer.stateMachine.states)
                    yield return child.state;

                foreach (var sub in layer.stateMachine.stateMachines)
                    foreach (var child in sub.stateMachine.states)
                        yield return child.state;
            }
        }

        [MenuItem("Tools/Idle Battle/Configure Character Skills")]
        public static void Configure()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                throw new System.InvalidOperationException($"Animator Controller not found: {ControllerPath}");

            EnsureParameter(controller, "Skill", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Num", AnimatorControllerParameterType.Int);

            var root = controller.layers[0].stateMachine;
            var idle = root.states.Select(x => x.state).FirstOrDefault(x => x.name == "CombatIdle");
            if (idle == null)
                throw new System.InvalidOperationException("CombatIdle state was not found.");

            RemoveLegacySkillState(root);

            var skillMachine = root.stateMachines
                .Select(x => x.stateMachine)
                .FirstOrDefault(x => x.name == "Skills");
            if (skillMachine == null)
                skillMachine = root.AddStateMachine("Skills", new Vector3(540f, 230f));

            ClearStateMachine(skillMachine);

            var positions = new[]
            {
                new Vector3(260f, 40f),
                new Vector3(520f, 40f),
                new Vector3(260f, 170f),
                new Vector3(520f, 170f),
                new Vector3(390f, 300f)
            };

            for (var i = 0; i < SkillClipPaths.Length; i++)
            {
                var clip = LoadMainAnimationClip(SkillClipPaths[i]);
                if (clip == null)
                    throw new System.InvalidOperationException($"Animation clip not found: {SkillClipPaths[i]}");

                var state = skillMachine.AddState($"Skill{i + 1}", positions[i]);
                state.motion = clip;

                var returnTransition = state.AddTransition(idle);
                returnTransition.hasExitTime = true;
                returnTransition.exitTime = .95f;
                returnTransition.duration = .1f;
                returnTransition.hasFixedDuration = true;

                var dispatch = root.AddAnyStateTransition(state);
                dispatch.AddCondition(AnimatorConditionMode.If, 0f, "Skill");
                dispatch.AddCondition(AnimatorConditionMode.Equals, i, "Num");
                dispatch.hasExitTime = false;
                dispatch.duration = .08f;
                dispatch.canTransitionToSelf = false;
            }

            skillMachine.defaultState = skillMachine.states[0].state;
            // CombatIdle · Run은 여기서 다시 만들지 않으므로 참조만 확인해 줍니다.
            RepairMissingMotions(controller);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Character Animator skills configured: Num 0~4 -> 1-1, 1-2, 1-3, 1-4, 1-5.");
        }

        public static void ConfigureBatchMode()
        {
            Configure();
            EditorApplication.Exit(0);
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            var parameter = controller.parameters.FirstOrDefault(x => x.name == name);
            if (parameter == null)
            {
                controller.AddParameter(name, type);
                return;
            }

            if (parameter.type != type)
                throw new System.InvalidOperationException(
                    $"Animator parameter '{name}' must be {type}, but is {parameter.type}.");
        }

        private static void RemoveLegacySkillState(AnimatorStateMachine root)
        {
            var legacy = root.states.Select(x => x.state).FirstOrDefault(x => x.name == "Skill");
            if (legacy != null)
                root.RemoveState(legacy);

            foreach (var transition in root.anyStateTransitions.ToArray())
                if (transition.conditions.Any(x => x.parameter == "Skill"))
                    root.RemoveAnyStateTransition(transition);
        }

        private static void ClearStateMachine(AnimatorStateMachine stateMachine)
        {
            foreach (var state in stateMachine.states.Select(x => x.state).ToArray())
                stateMachine.RemoveState(state);
            foreach (var child in stateMachine.stateMachines.Select(x => x.stateMachine).ToArray())
                stateMachine.RemoveStateMachine(child);
        }

        private static AnimationClip LoadMainAnimationClip(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(x => !x.name.StartsWith("__preview__", System.StringComparison.Ordinal));
        }
    }
}
