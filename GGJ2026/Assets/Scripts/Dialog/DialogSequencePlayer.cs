using System;
using System.Collections.Generic;
using Mask;
using UnityEngine;

namespace Dialog
{
    /// <summary>
    /// 对话序列播放器：依次播放多段 DialogScript，仅依赖 DialogManager.Play(script, callback)。
    /// </summary>
    public class DialogSequencePlayer : MonoBehaviour
    {
        public sealed class SequenceStep
        {
            public DialogScript Script { get; }
            public Action Command { get; }

            private SequenceStep(DialogScript script, Action command)
            {
                Script = script;
                Command = command;
            }

            public static SequenceStep FromScript(DialogScript script) => new SequenceStep(script, null);
            public static SequenceStep FromCommand(Action command) => new SequenceStep(null, command);
        }

        [SerializeField] private DialogManager dialogManager;
        [SerializeField] private MaskSystem maskSystem;

        private readonly Queue<DialogScript> _queue = new Queue<DialogScript>();
        private readonly Queue<SequenceStep> _stepQueue = new Queue<SequenceStep>();
        private Action _sequenceFinished;

        public void PlaySequence(IEnumerable<DialogScript> scripts, Action finished = null)
        {
            if (dialogManager == null)
            {
                finished?.Invoke();
                return;
            }

            _queue.Clear();
            int totalLines = 0;
            foreach (var s in scripts)
            {
                if (s != null && s.lines != null && s.lines.Count > 0)
                {
                    _queue.Enqueue(s);
                    totalLines += s.lines.Count;
                }
            }

            _sequenceFinished = finished;

            if (_queue.Count == 0)
            {
                _sequenceFinished?.Invoke();
                return;
            }

            // if (maskSystem != null)
            //     maskSystem.InitSession(totalLines);

            PlayNextInQueue();
        }

        public void PlaySequence(IEnumerable<SequenceStep> steps, Action finished = null)
        {
            if (dialogManager == null)
            {
                finished?.Invoke();
                return;
            }

            _stepQueue.Clear();
            foreach (var step in steps)
            {
                if (step == null)
                    continue;

                if (step.Script == null && step.Command == null)
                    continue;

                if (step.Script != null && (step.Script.lines == null || step.Script.lines.Count == 0))
                    continue;

                _stepQueue.Enqueue(step);
            }

            _sequenceFinished = finished;
            if (_stepQueue.Count == 0)
            {
                _sequenceFinished?.Invoke();
                return;
            }

            PlayNextStep();
        }

        private void PlayNextInQueue()
        {
            if (_queue.Count == 0)
            {
                var cb = _sequenceFinished;
                _sequenceFinished = null;
                cb?.Invoke();
                return;
            }

            var next = _queue.Dequeue();
            dialogManager.Play(next, PlayNextInQueue);
        }

        private void PlayNextStep()
        {
            if (_stepQueue.Count == 0)
            {
                var cb = _sequenceFinished;
                _sequenceFinished = null;
                cb?.Invoke();
                return;
            }

            var next = _stepQueue.Dequeue();
            if (next.Script != null)
            {
                dialogManager.Play(next.Script, PlayNextStep);
                return;
            }

            next.Command?.Invoke();
            PlayNextStep();
        }
    }
}
