using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // The scene owns the Animator and clips. The mode view supplies one clock so a lesson,
    // input interruption and travel cannot race independent Animator transitions.
    public sealed class DojoActorAnimation : MonoBehaviour
    {
        [Serializable]
        public sealed class Pose
        {
            public DojoIdleVariation.Motion motion;
            public AnimationClip clip;
        }

        public Animator animator;
        public Image artwork;
        public Pose[] poses = Array.Empty<Pose>();
        public DojoIdleVariation.Motion Current { get; private set; }

        public void Validate()
        {
            if (!animator || !artwork || !animator.runtimeAnimatorController)
            {
                throw new InvalidOperationException("Dojo sprite Animator composition missing.");
            }
            if (poses == null || poses.Length == 0)
            {
                throw new InvalidOperationException("Dojo animation clips missing.");
            }
            foreach (var pose in poses)
            {
                if (pose == null || !pose.clip)
                {
                    throw new InvalidOperationException("Dojo animation clip missing.");
                }
                if (pose.clip.length <= 0)
                {
                    throw new InvalidOperationException("Dojo animation clip has no duration.");
                }
            }
            Duration(DojoIdleVariation.Motion.None);
        }

        public float Duration(DojoIdleVariation.Motion motion)
        {
            return Find(motion).length;
        }

        public void Sample(DojoIdleVariation.Motion motion, float seconds)
        {
            var clip = Find(motion);
            animator.enabled = true;
            animator.speed = 0;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            float progress = Mathf.Clamp01(seconds / clip.length);
            if (motion == DojoIdleVariation.Motion.None)
            {
                progress = Mathf.Repeat(seconds / clip.length, 1);
            }
            animator.Play(Animator.StringToHash(clip.name), 0, progress);
            animator.Update(0);
            Current = motion;
        }

        public void Stop()
        {
            if (animator)
            {
                animator.enabled = false;
            }
            Current = DojoIdleVariation.Motion.None;
        }

        private AnimationClip Find(DojoIdleVariation.Motion motion)
        {
            foreach (var pose in poses)
            {
                if (pose.motion == motion)
                {
                    return pose.clip;
                }
            }
            throw new InvalidOperationException("Dojo animation missing: " + motion);
        }

        private void OnDisable()
        {
            Stop();
        }
    }
}
