using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class BreedingManager : MonoBehaviour
    {
        /// <summary>
        /// Palworld-like base calculation:
        /// floor((parentA + parentB) / 2) + correction.
        /// The actual child monster is resolved later from the monster database
        /// using the closest breeding rank/value.
        /// </summary>
        public int CalculateChildBreedingValue(int parentAValue, int parentBValue, int correction)
        {
            long average = ((long)parentAValue + parentBValue) / 2L;
            long result = average + correction;

            if (result > int.MaxValue) return int.MaxValue;
            if (result < int.MinValue) return int.MinValue;
            return (int)result;
        }
    }
}