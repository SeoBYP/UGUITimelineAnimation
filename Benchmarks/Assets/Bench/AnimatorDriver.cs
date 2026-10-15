using UnityEngine;

namespace Bench
{
    /// <summary>
    /// Unity Animator: a controller with one clip per motion (made by BenchBuild into Resources), an Animator per row.
    /// No per-character text animation (Text is not supported).
    /// </summary>
    public sealed class AnimatorDriver : IBenchDriver
    {
        public Approach Approach => Approach.Animator;
        public bool Supports(Scenario scenario) => scenario != Scenario.Text;

        Animator[] _animators;
        string _state;

        public void Setup(Scenario scenario, BenchItems items)
        {
            _state = "Bench" + scenario;
            var controller = Resources.Load<RuntimeAnimatorController>(_state);
            _animators = new Animator[items.Count];
            for (var i = 0; i < items.Count; i++)
            {
                var animator = items.bodies[i].gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _animators[i] = animator;
            }
        }

        public void Replay()
        {
            foreach (var a in _animators) a.Play(_state, 0, 0f);
        }

        public void Tick(float time) { }
        public void Teardown() { }
    }
}
