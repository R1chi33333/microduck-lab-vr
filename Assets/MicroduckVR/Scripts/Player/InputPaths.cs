namespace MicroduckVR.Player
{
    /// <summary>Input System binding paths: the Touch Pro / Touch controllers, and the keyboard for desk testing.</summary>
    public static class InputPaths
    {
        // ---- controllers
        public const string LeftStick = "<XRController>{LeftHand}/{Primary2DAxis}", RightStick = "<XRController>{RightHand}/{Primary2DAxis}";
        public const string LeftStickClick = "<XRController>{LeftHand}/{Primary2DAxisClick}", RightStickClick = "<XRController>{RightHand}/{Primary2DAxisClick}";
        /// <summary>X and Y on the left controller, A and B on the right.</summary>
        public const string LeftPrimary = "<XRController>{LeftHand}/{PrimaryButton}", LeftSecondary = "<XRController>{LeftHand}/{SecondaryButton}";
        public const string RightPrimary = "<XRController>{RightHand}/{PrimaryButton}", RightSecondary = "<XRController>{RightHand}/{SecondaryButton}";
        public const string LeftTrigger = "<XRController>{LeftHand}/trigger", RightTrigger = "<XRController>{RightHand}/trigger";
        public const string LeftGrip = "<XRController>{LeftHand}/grip", RightGrip = "<XRController>{RightHand}/grip";
        public const string LeftMenu = "<XRController>{LeftHand}/menu", LeftMenuButton = "<XRController>{LeftHand}/menuButton";

        // ---- keyboard
        public const string KeyW = "<Keyboard>/w", KeyA = "<Keyboard>/a", KeyS = "<Keyboard>/s", KeyD = "<Keyboard>/d";
        public const string KeyQ = "<Keyboard>/q", KeyE = "<Keyboard>/e", KeyR = "<Keyboard>/r", KeyF = "<Keyboard>/f";
        public const string KeyC = "<Keyboard>/c", KeyK = "<Keyboard>/k", KeyL = "<Keyboard>/l", KeyT = "<Keyboard>/t", KeyV = "<Keyboard>/v", KeyX = "<Keyboard>/x";
        public const string KeySpace = "<Keyboard>/space", KeyTab = "<Keyboard>/tab", KeyEnter = "<Keyboard>/enter", KeyEscape = "<Keyboard>/escape";
        public const string KeyLeftShift = "<Keyboard>/leftShift", KeyLeftCtrl = "<Keyboard>/leftCtrl";
        public const string KeyUp = "<Keyboard>/upArrow", KeyDown = "<Keyboard>/downArrow", KeyLeft = "<Keyboard>/leftArrow", KeyRight = "<Keyboard>/rightArrow";
    }
}
