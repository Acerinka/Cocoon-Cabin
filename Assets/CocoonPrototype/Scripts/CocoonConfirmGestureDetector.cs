using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace CocoonPrototype
{
    public sealed class CocoonConfirmGestureDetector : MonoBehaviour
    {
        private readonly List<InputDevice> devices = new List<InputDevice>();
        private readonly List<InputDevice> handTrackingDevices = new List<InputDevice>();
        private readonly List<InputDevice> fallbackDevices = new List<InputDevice>();
        private bool wasPressed;
        private bool lastPressed;
        private int lastDeviceCount = -1;

        public bool ConsumeConfirmDown()
        {
            bool pressed = ReadPressed();
            bool down = pressed && !wasPressed;
            wasPressed = pressed;

            bool keyboardConfirm = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.cKey.wasPressedThisFrame;
            bool mouseConfirm = UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
            if (keyboardConfirm || mouseConfirm)
            {
                down = true;
            }

            if (pressed != lastPressed)
            {
                CocoonDebugLog.Verbose("ConfirmInput", "Confirm input " + (pressed ? "pressed" : "released") + ".", this);
                lastPressed = pressed;
            }

            if (down)
            {
                CocoonDebugLog.Verbose("ConfirmInput", "Confirm down consumed. keyboard=" + keyboardConfirm + ", mouse=" + mouseConfirm + ".", this);
            }

            return down;
        }

        private bool ReadPressed()
        {
            devices.Clear();
            AddDeviceIfValid(InputDevices.GetDeviceAtXRNode(XRNode.RightHand));

            fallbackDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, fallbackDevices);
            for (int i = 0; i < fallbackDevices.Count; i++)
            {
                AddDeviceIfValid(fallbackDevices[i]);
            }

            handTrackingDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.HandTracking, handTrackingDevices);
            for (int i = 0; i < handTrackingDevices.Count; i++)
            {
                AddDeviceIfValid(handTrackingDevices[i]);
            }

            if (devices.Count == 0)
            {
                fallbackDevices.Clear();
                InputDevices.GetDevices(fallbackDevices);
                for (int i = 0; i < fallbackDevices.Count; i++)
                {
                    if (IsCandidateConfirmDevice(fallbackDevices[i]))
                    {
                        AddDeviceIfValid(fallbackDevices[i]);
                    }
                }
            }

            if (devices.Count != lastDeviceCount)
            {
                CocoonDebugLog.Verbose("ConfirmInput", "Right confirm devices=" + devices.Count + ".", this);
                lastDeviceCount = devices.Count;
            }

            for (int i = 0; i < devices.Count; i++)
            {
                if (TryReadButton(devices[i], CommonUsages.triggerButton) ||
                    TryReadButton(devices[i], CommonUsages.gripButton) ||
                    TryReadButton(devices[i], CommonUsages.primaryButton) ||
                    TryReadButton(devices[i], CommonUsages.secondaryButton))
                {
                    return true;
                }

                if (devices[i].TryGetFeatureValue(CommonUsages.trigger, out float triggerValue) && triggerValue > 0.72f)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddDeviceIfValid(InputDevice device)
        {
            if (device.isValid && !devices.Contains(device))
            {
                devices.Add(device);
            }
        }

        private static bool IsCandidateConfirmDevice(InputDevice device)
        {
            if (!device.isValid)
            {
                return false;
            }

            InputDeviceCharacteristics characteristics = device.characteristics;
            bool headMounted = (characteristics & InputDeviceCharacteristics.HeadMounted) != 0;
            bool controllerLike =
                (characteristics & InputDeviceCharacteristics.Controller) != 0 ||
                (characteristics & InputDeviceCharacteristics.HeldInHand) != 0 ||
                (characteristics & InputDeviceCharacteristics.HandTracking) != 0;
            return controllerLike && !headMounted;
        }

        private static bool TryReadButton(InputDevice device, InputFeatureUsage<bool> usage)
        {
            return device.TryGetFeatureValue(usage, out bool value) && value;
        }
    }
}
