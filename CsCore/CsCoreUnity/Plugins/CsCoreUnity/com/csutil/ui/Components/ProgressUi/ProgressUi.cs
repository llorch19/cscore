﻿using UnityEngine;
using UnityEngine.UI;
using System;
using com.csutil.ui;
using UnityEngine.Events;

namespace com.csutil.progress {

    public abstract class ProgressUi : MonoBehaviour {

        /// <summary> Optional text that will show the current progress values </summary>
        public Text progressText;
        /// <summary> If true will look for a CanvasGroupFader in parents to fade based on total progress </summary>
        public bool enableProgressUiFading = true;
        /// <summary> After this delay the finished progress will be cleared once all progresses are 100 so 
        /// that a clean start can be visualized once the next wave of progresses happen. Disabled if < 0 </summary>
        public int delayInMsBeforeProgressCleanup = 2000;

        public UnityAction onProgressUiComplete;

        /// <summary> The progress manage the UI will use as the source, will try to inject if not set manually </summary>
        public ProgressManager progressManager;

        private CanvasGroupFader canvasGroupFader;

        private void OnEnable() {
            this.ExecuteDelayed(RegisterWithProgressManager, delayInMsBeforeExecution: 100); // Wait for manager to exist
        }

        private void RegisterWithProgressManager() {
            if (progressManager == null) { progressManager = IoC.inject.Get<ProgressManager>(this); }
            if (progressManager == null) { throw new NullReferenceException("No ProgressManager available"); }
            progressManager.OnProgressUpdate += OnProgressUpdate;
            OnProgressUpdate(progressManager, null);
        }

        private void OnDisable() {
            if (progressManager != null) { progressManager.OnProgressUpdate -= OnProgressUpdate; }
        }

        private void OnProgressUpdate(object sender, IProgress _) {
            AssertV2.IsTrue(sender == progressManager, "sender != pm (ProgressManager field)");
            var percent = Math.Round(progressManager.combinedAvgPercent, 3);
            SetPercentInUi(percent);
            if (progressText != null) {
                progressText.text = $"{percent}% ({progressManager.combinedCount}/{progressManager.combinedTotalCount})";
            }

            // Handle progress UI fading:
            if (enableProgressUiFading) {
                if (canvasGroupFader == null) {
                    canvasGroupFader = GetProgressUiGo().GetComponentInParents<CanvasGroupFader>();
                }
                if (percent == 0 || percent >= 100) {
                    canvasGroupFader.targetAlpha = 0;
                } else {
                    canvasGroupFader.targetAlpha = canvasGroupFader.initialAlpha;
                }
            }

            if (percent >= 100 && delayInMsBeforeProgressCleanup >= 0) {
                this.ExecuteDelayed(ResetProgressManagerIfAllFinished, delayInMsBeforeProgressCleanup);
                onProgressUiComplete?.Invoke();
            }
        }

        /// <summary> If currently all progresses of the manager are finished will remove all of them </summary>
        private void ResetProgressManagerIfAllFinished() {
            if (progressManager.combinedPercent >= 100) {
                progressManager.RemoveProcesses(progressManager.GetCompletedProgresses());
            }
        }

        protected abstract GameObject GetProgressUiGo();

        /// <summary> Called whenever the progress changes </summary>
        /// <param name="percent"> A value from 0 to 100 </param>
        protected abstract void SetPercentInUi(double percent);

    }

}