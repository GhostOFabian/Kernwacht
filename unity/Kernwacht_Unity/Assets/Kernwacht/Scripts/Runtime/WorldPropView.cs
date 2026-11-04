namespace Kernwacht.Models
{
    /// <summary>Weltobjekt (Kern, Portal, Laden, Hindernisse): Idle läuft, Attack = Treffer/Ereignis.</summary>
    public class WorldPropView : ModelAnimator
    {
        /// <summary>Ereignis abspielen (Kern getroffen, Gegner tritt aus dem Portal).</summary>
        public void Pulse() => PlayAttack();
    }
}
