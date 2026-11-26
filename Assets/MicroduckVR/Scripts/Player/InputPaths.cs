namespace MicroduckVR.Player
{
    /// <summary>Input System binding paths: the Touch Pro / Touch controllers, and the keyboard for desk testing.</summary>
    public static class InputPaths
    {
        // ---- controllers
        /// <summary>Thumbsticks.</summary>
        public const string LeftStick = "<XRController>{LeftHand}/{Primary2DAxis}", RightStick = "<XRController>{RightHand}/{Primary2DAxis}";
        /// <summary>Thumbstick clicks.</summary>
        public const string LeftStickClick = "<XRController>{LeftHand}/{Primary2DAxisClick}", RightStickClick = "<XRController>{RightHand}/{Primary2DAxisClick}";
        /// <summary>X and Y on the left controller, A and B on the right.</summary>
        public const string LeftPrimary = "<XRController>{LeftHand}/{PrimaryButton}", LeftSecondary = "<XRController>{LeftHand}/{SecondaryButton}";
        /// <summary>A and B on the right controller.</summary>
        public const string RightPrimary = "<XRController>{RightHand}/{PrimaryButton}", RightSecondary = "<XRController>{RightHand}/{SecondaryButton}";
        /// <summary>Triggers.</summary>
        public const string LeftTrigger = "<XRController>{LeftHand}/trigger", RightTrigger = "<XRController>{RightHand}/trigger";
        /// <summary>Grips.</summary>
        public const string LeftGrip = "<XRController>{LeftHand}/grip", RightGrip = "<XRController>{RightHand}/grip";
        /// <summary>The left menu button (two names for it across controller profiles).</summary>
        public const string LeftMenu = "<XRController>{LeftHand}/menu", LeftMenuButton = "<XRController>{LeftHand}/menuButton";

        // ---- keyboard
        /// <summary>W A S D (walk).</summary>
        public const string KeyW = "<Keyboard>/w", KeyA = "<Keyboard>/a", KeyS = "<Keyboard>/s", KeyD = "<Keyboard>/d";
        /// <summary>Q E R F (turn and look).</summary>
        public const string KeyQ = "<Keyboard>/q", KeyE = "<Keyboard>/e", KeyR = "<Keyboard>/r", KeyF = "<Keyboard>/f";
        /// <summary>Other letter keys for desk testing.</summary>
        public const string KeyC = "<Keyboard>/c", KeyK = "<Keyboard>/k", KeyL = "<Keyboard>/l", KeyT = "<Keyboard>/t", KeyV = "<Keyboard>/v", KeyX = "<Keyboard>/x";
        /// <summary>Space, Tab, Enter and Escape.</summary>
        public const string KeySpace = "<Keyboard>/space", KeyTab = "<Keyboard>/tab", KeyEnter = "<Keyboard>/enter", KeyEscape = "<Keyboard>/escape";
        /// <summary>Left Shift and Left Ctrl (skate drift and boost).</summary>
        public const string KeyLeftShift = "<Keyboard>/leftShift", KeyLeftCtrl = "<Keyboard>/leftCtrl";
        /// <summary>Arrow keys.</summary>
        public const string KeyUp = "<Keyboard>/upArrow", KeyDown = "<Keyboard>/downArrow", KeyLeft = "<Keyboard>/leftArrow", KeyRight = "<Keyboard>/rightArrow";
    }
}
