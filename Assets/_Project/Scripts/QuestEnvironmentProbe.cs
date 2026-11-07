using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace ShuQi.VR
{
    // Diagnostic scene only: pose tracking is handled by Unity's TrackedPoseDriver.
    public sealed class QuestEnvironmentProbe : MonoBehaviour
    {
        public Text statusText;
        public Renderer leftController;
        public Renderer rightController;
        public Renderer leftTestObject;
        public Renderer rightTestObject;

        readonly StringBuilder text = new StringBuilder(700);
        MaterialPropertyBlock colors;
        bool leftPrimaryWasDown, rightPrimaryWasDown, pauseWasDown;
        bool paused;
        float nextTextUpdate;
        float smoothedDelta = 1f / 72f;

        void Awake()
        {
            colors = new MaterialPropertyBlock();
            if (!statusText || !leftController || !rightController || !leftTestObject || !rightTestObject)
            {
                Debug.LogError("Quest environment probe is missing a scene reference.", this);
                enabled = false;
                return;
            }
            Application.targetFrameRate = 72;
        }

        void Update()
        {
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool leftTracked = IsTracked(left), rightTracked = IsTracked(right);
            leftController.enabled = leftTracked;
            rightController.enabled = rightTracked;

            bool pauseDown = Button(right, CommonUsages.secondaryButton);
            if (pauseDown && !pauseWasDown && Application.isFocused)
                SetPaused(!paused);
            pauseWasDown = pauseDown;

            bool leftPrimary = Button(left, CommonUsages.primaryButton);
            bool rightPrimary = Button(right, CommonUsages.primaryButton);
            if (!paused)
            {
                Tint(leftTestObject, new Color(0.12f, 0.68f, 0.80f), Axis(left, CommonUsages.trigger));
                Tint(rightTestObject, new Color(0.95f, 0.47f, 0.16f), Axis(right, CommonUsages.trigger));
                if (leftPrimary && !leftPrimaryWasDown) Pulse(left);
                if (rightPrimary && !rightPrimaryWasDown) Pulse(right);
            }
            leftPrimaryWasDown = leftPrimary;
            rightPrimaryWasDown = rightPrimary;
            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, 0.04f);
            if (Time.unscaledTime < nextTextUpdate) return;
            nextTextUpdate = Time.unscaledTime + 0.15f;

            text.Clear();
            text.AppendLine("SHUQI  /  QUEST 3 ENVIRONMENT CHECK");
            text.Append(paused ? "PAUSED - press right B to resume" : "RUNNING");
            text.Append("    FPS: ").Append(Mathf.RoundToInt(1f / Mathf.Max(0.001f, smoothedDelta)));
            text.AppendLine();
            text.Append("Head: ").Append(IsTracked(head) ? head.name : "not tracked").AppendLine();
            AppendHand("LEFT", left, leftTracked);
            AppendHand("RIGHT", right, rightTracked);
            text.AppendLine("Trigger: change cube color  |  X / A: haptic pulse");
            text.AppendLine("Right B: pause / resume  |  System menu: safe pause");
            if (!head.isValid)
                text.Append("Desktop view: connect an XR runtime or deploy the APK to Quest 3.");
            else
                text.Append("Check head motion, controller alignment, reach and comfort in the headset.");
            statusText.text = text.ToString();
        }

        void AppendHand(string label, InputDevice device, bool tracked)
        {
            device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick);
            text.Append(label).Append(tracked ? " tracked" : " not tracked")
                .Append("  trigger ").Append(Axis(device, CommonUsages.trigger).ToString("0.00"))
                .Append("  grip ").Append(Axis(device, CommonUsages.grip).ToString("0.00"))
                .Append("  stick ").Append(stick.ToString("F2"))
                .Append("  primary ").Append(Button(device, CommonUsages.primaryButton) ? "ON" : "off")
                .AppendLine();
        }

        void Tint(Renderer target, Color idle, float amount)
        {
            target.GetPropertyBlock(colors);
            colors.SetColor("_BaseColor", Color.Lerp(idle, Color.white, amount));
            target.SetPropertyBlock(colors);
        }

        static bool IsTracked(InputDevice device) => device.isValid &&
            device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
        static bool Button(InputDevice device, InputFeatureUsage<bool> usage) => device.isValid &&
            device.TryGetFeatureValue(usage, out bool pressed) && pressed;
        static float Axis(InputDevice device, InputFeatureUsage<float> usage) => device.isValid &&
            device.TryGetFeatureValue(usage, out float value) ? value : 0f;

        static void Pulse(InputDevice device)
        {
            if (IsTracked(device) && device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
                device.SendHapticImpulse(0, 0.35f, 0.10f);
        }

        void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = paused ? 0f : 1f;
            nextTextUpdate = 0f;
        }

        void OnApplicationPause(bool value) { if (value) SetPaused(true); }
        void OnApplicationFocus(bool value) { if (!value) SetPaused(true); }
        void OnDisable() { Time.timeScale = 1f; }
    }
}
