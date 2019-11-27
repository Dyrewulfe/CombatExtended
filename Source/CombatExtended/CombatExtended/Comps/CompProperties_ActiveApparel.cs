using Verse;

namespace CombatExtended
{
    public class CompProperties_ActiveApparel : CompProperties
    {
        public bool defaultActive = true;
        public float activationTime = 1.0f;
        public bool throwMote = true;

        public CompProperties_ActiveApparel()
        {
            compClass = typeof(CompActiveApparel);
        }
    }
}
