using System;
using Dialog;
using Mask;
using UnityEngine;
using UnityEngine.Events;

namespace DefaultNamespace
{
    public class Prologue : MonoBehaviour
    {
        [Header("Dialog")]
        [SerializeField] private DialogSequencePlayer sequencePlayer;
        [SerializeField] private DialogScript prologue;
        [SerializeField] private DialogScript act2;
        [SerializeField] private PlayerController2D playerController;
        [SerializeField] private MaskSystem maskSystem;
        [Header("Commands Between Prologue And Act2")]
        [SerializeField] private GameObject[] activateBeforeAct2;
        [SerializeField] private GameObject[] deactivateBeforeAct2;
        [SerializeField] private Transform characterToMoveBeforeAct2;
        [SerializeField] private Vector3 characterTargetPositionBeforeAct2;
        [SerializeField] private Vector3 characterTargetScaleBeforeAct2;
        [SerializeField] private UnityEvent beforeAct2Commands;
        [Header("Movement Hint")]
        [SerializeField] private MovementHintUI movementHintUI;

        private void Start()
        {
            var interactorComp = playerController.GetComponent<Interactor2D>();
            playerController.EnableMovement(false);

            GlobalVariables.Instance.SetBool("g_CanUseGaze", false);
            GlobalVariables.Instance.SetBool("g_AllowAdvance", true);
            GlobalVariables.Instance.SetBool("g_GazeTutorialActive", false);
            GlobalVariables.Instance.SetBool("showMaskEnergyUI", false);
            GlobalVariables.Instance.SetBool("g_MaskFailFirstPlayed", false);
            maskSystem.InitSession(5);

            sequencePlayer.PlaySequence(new[]
            {
                DialogSequencePlayer.SequenceStep.FromScript(prologue),
                DialogSequencePlayer.SequenceStep.FromCommand(RunCommandsBeforeAct2),
                DialogSequencePlayer.SequenceStep.FromScript(act2),
            }, () =>
            {
                interactorComp?.EndInteraction();
                playerController.EnableMovement(true);
                if (movementHintUI)
                    movementHintUI.ShowWithFade();
            });
        }

        private void RunCommandsBeforeAct2()
        {
            foreach (var go in activateBeforeAct2)
            {
                if (go != null)
                    go.SetActive(true);
            }

            foreach (var go in deactivateBeforeAct2)
            {
                if (go != null)
                    go.SetActive(false);
            }

            if (characterToMoveBeforeAct2 != null)
                characterToMoveBeforeAct2.position = characterTargetPositionBeforeAct2;
            // if (characterToMoveBeforeAct2 != null)
            //     characterToMoveBeforeAct2.localScale = characterTargetScaleBeforeAct2;
            beforeAct2Commands?.Invoke();
        }
    }
}