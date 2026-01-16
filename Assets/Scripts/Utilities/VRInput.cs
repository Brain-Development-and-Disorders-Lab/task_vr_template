using UnityEngine;
using System.Collections;
using System.Threading.Tasks;
using System;

namespace Utilities
{
    /// <summary>
    /// Struct to manage input states captured during experiment
    /// </summary>
    public struct InputState
    {
        // Left controller state
        public float LeftTrigger { get; set; }
        public Vector2 LeftJoystick { get; set; }
        public bool YButton { get; set; }
        public bool XButton { get; set; }

        // Right controller state
        public float RightTrigger { get; set; }
        public Vector2 RightJoystick { get; set; }
        public bool BButton { get; set; }
        public bool AButton { get; set; }

        public InputState(float leftTrigger, Vector2 leftJoystick, bool y, bool x, float rightTrigger, Vector2 rightJoystick, bool b, bool a)
        {
            // Store left controller state
            LeftTrigger = leftTrigger;
            LeftJoystick = leftJoystick;
            YButton = y;
            XButton = x;

            // Store right controller state
            RightTrigger = rightTrigger;
            RightJoystick = rightJoystick;
            BButton = b;
            AButton = a;
        }
    }

    public static class VRInput
    {
        /// <summary>
        /// Stimulate controller haptics using frequency and amplitude parameters
        /// </summary>
        /// <param name="frequency">Frequency of the vibration</param>
        /// <param name="amplitude">Amplitude of the vibration</param>
        /// <param name="duration">Duration of the haptic</param>
        /// <param name="left">Enable on the left controller</param>
        /// <param name="right">Enable on the right controller</param>
        /// <returns></returns>
        public static async void SetHaptics(float frequency, float amplitude, float duration, bool left, bool right)
        {
            // Set controller vibration to specified parameters
            if (left)
            {
                OVRInput.SetControllerVibration(frequency, amplitude, OVRInput.Controller.LTouch);
            }
            if (right)
            {
                OVRInput.SetControllerVibration(frequency, amplitude, OVRInput.Controller.RTouch);
            }

            // Wait the specified duration (seconds)
            await Task.Delay(TimeSpan.FromSeconds(duration));

            // Reset controller vibration
            if (left)
            {
                OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
            }
            if (right)
            {
                OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
            }
        }

        /// <summary>
        /// Return a structure representing the current input state
        /// </summary>
        /// <returns></returns>
        public static InputState PollAllInput()
        {
            // Simplify directional joystick input and translate keyboard input
            // Raw input represented as Vector2 along X/Y axis with values [-1.0f, 1.0f]
            var leftJoystickRaw = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);
            var rightJoystickRaw = OVRInput.Get(OVRInput.RawAxis2D.RThumbstick);

            // Establish if joysticks are "constrained" along a specific axis
            bool leftXConstrained = leftJoystickRaw.y is > (-0.2f) and < 0.2f;
            bool leftYConstrained = leftJoystickRaw.x is > (-0.2f) and < 0.2f;
            bool rightXConstrained = rightJoystickRaw.y is > (-0.2f) and < 0.2f;
            bool rightYConstrained = rightJoystickRaw.x is > (-0.2f) and < 0.2f;

            // Generate simplified vectors to return as input values
            Vector2 leftJoystickDirection = new();
            Vector2 rightJoystickDirection = new();

            // Vertical keyboard inputs
            if (Input.GetKey(KeyCode.DownArrow))
            {
                leftJoystickDirection.x = 0.0f;
                leftJoystickDirection.y = -1.0f;
                rightJoystickDirection.x = 0.0f;
                rightJoystickDirection.y = -1.0f;
            }
            else if (Input.GetKey(KeyCode.UpArrow))
            {
                leftJoystickDirection.x = 0.0f;
                leftJoystickDirection.y = 1.0f;
                rightJoystickDirection.x = 0.0f;
                rightJoystickDirection.y = 1.0f;
            }

            // Horizontal keyboard inputs
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                leftJoystickDirection.x = -1.0f;
                leftJoystickDirection.y = 0.0f;
                rightJoystickDirection.x = -1.0f;
                rightJoystickDirection.y = 0.0f;
            }
            else if (Input.GetKey(KeyCode.RightArrow))
            {
                leftJoystickDirection.x = 1.0f;
                leftJoystickDirection.y = 0.0f;
                rightJoystickDirection.x = 1.0f;
                rightJoystickDirection.y = 0.0f;
            }

            // Check the raw joystick inputs to see if they override the keyboard inputs
            // Left joystick
            if (leftYConstrained && leftJoystickRaw.y != 0.0f)
            {
                leftJoystickDirection.x = 0.0f;
                leftJoystickDirection.y = leftJoystickRaw.y;
            }
            else if (leftXConstrained && leftJoystickRaw.x != 0.0f)
            {
                leftJoystickDirection.x = leftJoystickRaw.x;
                leftJoystickDirection.y = 0.0f;
            }

            // Right joystick
            if (rightYConstrained && rightJoystickRaw.y != 0.0f)
            {
                rightJoystickDirection.x = 0.0f;
                rightJoystickDirection.y = rightJoystickRaw.y;
            }
            else if (rightXConstrained && rightJoystickRaw.x != 0.0f)
            {
                rightJoystickDirection.x = rightJoystickRaw.x;
                rightJoystickDirection.y = 0.0f;
            }

            return new InputState(
                Input.GetKey(KeyCode.Alpha2) ? 1.0f : OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger),
                leftJoystickDirection,
                OVRInput.Get(OVRInput.RawButton.Y),
                OVRInput.Get(OVRInput.RawButton.X) || Input.GetKey(KeyCode.Alpha3),
                Input.GetKey(KeyCode.Alpha7) ? 1.0f : OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger),
                rightJoystickDirection,
                OVRInput.Get(OVRInput.RawButton.B),
                OVRInput.Get(OVRInput.RawButton.A) || Input.GetKey(KeyCode.Alpha6)
            );
        }

        /// <summary>
        /// State of the left trigger in relation to a default threshold value of 0.8f
        /// </summary>
        /// <returns>`true` if pressed beyond threshold value, `false` otherwise</returns>
        public static bool LeftTrigger(float threshold = 0.8f) => Input.GetKey(KeyCode.Alpha2) || OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger) > threshold;

        /// <summary>
        /// State of the right trigger in relation to a default threshold value of 0.8f
        /// </summary>
        /// <returns>`true` if pressed beyond threshold value, `false` otherwise</returns>
        public static bool RightTrigger(float threshold = 0.8f) => Input.GetKey(KeyCode.Alpha7) || OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger) > threshold;

        /// <summary>
        /// Flag if any inputs are active at one time
        /// </summary>
        /// <returns></returns>
        public static bool AnyInput()
        {
            var inputs = PollAllInput();
            return inputs.LeftTrigger != 0.0f || inputs.LeftJoystick.x != 0.0f || inputs.LeftJoystick.y != 0.0f ||
                inputs.YButton || inputs.XButton ||
                inputs.RightTrigger != 0.0f || inputs.RightJoystick.x != 0.0f || inputs.RightJoystick.y != 0.0f ||
                inputs.BButton || inputs.AButton;
        }
    }
}
