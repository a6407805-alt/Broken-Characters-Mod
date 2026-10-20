using System;
using Rewired;
using UnityEngine;

namespace DuelistMod
{
    /// <summary>
    /// Reads the first gamepad through Rewired's gamepad template (works for Xbox, PlayStation, Switch pads
    /// alike), with Unity's joystick buttons as a fallback. Polled once per frame by the duel UI.
    /// Names follow the Xbox layout: A (bottom), B (right), X (left), Y (top).
    /// </summary>
    public class DuelPad
    {
        public Vector2 Stick;
        public bool A, B, X, Y, LB, RB, LT, RT, Back, Up, Down, Left, Right;
        public bool AnyInput;

        private bool _templateBroken;
        private int _frame = -1;

        public void Poll()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            Stick = Vector2.zero;
            A = B = X = Y = LB = RB = LT = RT = Back = Up = Down = Left = Right = false;

            bool got = false;
            if (!_templateBroken)
            {
                try { got = PollRewired(); }
                catch (Exception e)
                {
                    _templateBroken = true;
                    DuelistPlugin.Log.LogWarning("Gamepad template unavailable, using basic joystick buttons: " + e.Message);
                }
            }
            if (!got) PollUnity();

            if (Stick.magnitude < 0.2f) Stick = Vector2.zero;    // dead zone
            AnyInput = Stick != Vector2.zero || A || B || X || Y || LB || RB || LT || RT || Back || Up || Down || Left || Right;
        }

        private bool PollRewired()
        {
            if (!ReInput.isReady) return false;
            foreach (Joystick j in ReInput.controllers.Joysticks)
            {
                if (j == null || !j.isConnected) continue;
                IGamepadTemplate g = j.GetTemplate<IGamepadTemplate>();
                if (g == null) continue;
                Stick = new Vector2(g.leftStick.horizontal.value, g.leftStick.vertical.value);
                A = g.actionBottomRow1.justPressed;
                B = g.actionBottomRow2.justPressed;
                X = g.actionTopRow1.justPressed;
                Y = g.actionTopRow2.justPressed;
                LB = g.leftShoulder1.justPressed;
                RB = g.rightShoulder1.justPressed;
                LT = g.leftShoulder2.justPressed;
                RT = g.rightShoulder2.justPressed;
                Back = g.center1.justPressed;
                Up = g.dPad.up.justPressed;
                Down = g.dPad.down.justPressed;
                Left = g.dPad.left.justPressed;
                Right = g.dPad.right.justPressed;
                return true;
            }
            return false;
        }

        private void PollUnity()
        {
            A = Input.GetKeyDown(KeyCode.JoystickButton0);
            B = Input.GetKeyDown(KeyCode.JoystickButton1);
            X = Input.GetKeyDown(KeyCode.JoystickButton2);
            Y = Input.GetKeyDown(KeyCode.JoystickButton3);
            LB = Input.GetKeyDown(KeyCode.JoystickButton4);
            RB = Input.GetKeyDown(KeyCode.JoystickButton5);
            Back = Input.GetKeyDown(KeyCode.JoystickButton6);
            try { Stick = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")); } catch { }
            // keyboard arrows also feed "Horizontal"; only count it as the stick if no key is held
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
                Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S)) Stick = Vector2.zero;
        }
    }
}
