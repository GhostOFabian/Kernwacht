namespace Kernwacht.Game
{
    /// <summary>
    /// Ersatz für das Browser-<c>KeyboardEvent</c>, wie ihn das Webspiel auswertet.
    /// <see cref="key"/> entspricht <c>KeyboardEvent.key.toLowerCase()</c> ("a", "1", " ", "escape", "pageup", "ß", "!" …),
    /// <see cref="code"/> dem physischen <c>KeyboardEvent.code</c> ("KeyA", "Digit1", "Numpad1", "Space" …).
    /// Erzeugt vom <see cref="InputController"/> aus <c>UnityEngine.InputSystem.Keyboard</c>.
    /// </summary>
    public class KeyEvt
    {
        public string key;
        public string code;
        public bool shiftKey, ctrlKey, altKey, metaKey, repeat;
        public bool defaultPrevented;

        public void preventDefault() { defaultPrevented = true; }

        public override string ToString() => $"KeyEvt({key}/{code}{(shiftKey ? " +Shift" : "")}{(ctrlKey ? " +Ctrl" : "")}{(altKey ? " +Alt" : "")}{(repeat ? " repeat" : "")})";
    }
}
