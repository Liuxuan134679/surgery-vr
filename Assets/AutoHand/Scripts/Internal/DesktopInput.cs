using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Autohand
{
    // Preserve the desktop examples' serialized KeyCode bindings with the Input System.
    public static class DesktopInput
    {
        public static bool GetKey(KeyCode key) => Button(key)?.isPressed ?? false;
        public static bool GetKeyDown(KeyCode key) => Button(key)?.wasPressedThisFrame ?? false;
        public static bool GetKeyUp(KeyCode key) => Button(key)?.wasReleasedThisFrame ?? false;

        static ButtonControl Button(KeyCode key)
        {
            switch(key)
            {
                case KeyCode.Mouse0: return Mouse.current?.leftButton;
                case KeyCode.Mouse1: return Mouse.current?.rightButton;
                case KeyCode.Mouse2: return Mouse.current?.middleButton;
                case KeyCode.Mouse3: return Mouse.current?.backButton;
                case KeyCode.Mouse4: return Mouse.current?.forwardButton;
            }
            var keyboard=Keyboard.current;
            if(keyboard==null||key==KeyCode.None)return null;
            string name=key.ToString();
            switch(key)
            {
                case KeyCode.Return: name="Enter";break;
                case KeyCode.LeftControl: name="LeftCtrl";break;
                case KeyCode.RightControl: name="RightCtrl";break;
                case KeyCode.KeypadPeriod: name="NumpadPeriod";break;
                case KeyCode.LeftCommand: case KeyCode.LeftWindows: name="LeftMeta";break;
                case KeyCode.RightCommand: case KeyCode.RightWindows: name="RightMeta";break;
                default: name=name.Replace("Alpha","Digit").Replace("Keypad","Numpad");break;
            }
            return Enum.TryParse(name,true,out Key mapped)&&mapped!=Key.None?keyboard[mapped]:null;
        }

        public static float GetAxis(string axis)
        {
            switch(axis)
            {
                case "Horizontal": return (GetKey(KeyCode.D)||GetKey(KeyCode.RightArrow)?1f:0f)-(GetKey(KeyCode.A)||GetKey(KeyCode.LeftArrow)?1f:0f);
                case "Vertical": return (GetKey(KeyCode.W)||GetKey(KeyCode.UpArrow)?1f:0f)-(GetKey(KeyCode.S)||GetKey(KeyCode.DownArrow)?1f:0f);
                // These examples previously used mouse axes with sensitivity 0.1.
                case "Mouse X": return Mouse.current==null?0f:Mouse.current.delta.ReadValue().x*.1f;
                case "Mouse Y": return Mouse.current==null?0f:Mouse.current.delta.ReadValue().y*.1f;
                default: return 0f;
            }
        }
    }
}
