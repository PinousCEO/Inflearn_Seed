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

        private static readonly string[] SkillClipPaths =
        {
            AnimationFolder + "/Skill01.fbx",
            AnimationFolder + "/Skill02.fbx",
            AnimationFolder + "/Skill03.fbx",
            AnimationFolder + "/Skill05.fbx"
        };

        [InitializeOnLoadMethod]
        private static void ConfigureOnceAfterCompile()
        {
            if (SessionState.GetBool(AutoSetupSessionKey, false))
                return;

            SessionState.SetBool(AutoSetupSessionKey, true);
            EditorApplication.delayCall += () =>
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                var alreadyConfigured = controller != null &&
                                        controller.parameters.Any(x =>
                                            x.name == "Num" &&
                                            x.type == AnimatorControllerParameterType.Int) &&
                                        controller.layers[0].stateMachine.stateMachines.Any(x =>
                                            x.stateMachine.name == "Skills" &&
                                            x.stateMachine.states.Length == SkillClipPaths.Length);
                if (!alreadyConfigured)
                    Configure();
            };
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
                new Vector3(520f, 170f)
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
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Character Animator skills configured: Num 0~3 -> Skill01, Skill02, Skill03, Skill05.");
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
