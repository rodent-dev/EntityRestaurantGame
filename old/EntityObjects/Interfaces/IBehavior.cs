using System;
using System.Collections.Generic;
using System.Text;

namespace EntityObjects.Interfaces
{
    public interface IBehavior
    {
        /// <summary>
        /// Subcategory name of the behavior, for something like aggression this can be "pride" or "wrath" 
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Type of the behavior as it relates to actions
        /// </summary>
        public string BehaviorType { get; }

        /// <summary>
        /// The dynamic modifier for this behavior
        /// </summary>
        public int Scalar { get; set; }

        /// <summary>
        /// How strong this behavior is as it relates to others of its type.
        /// IE Timid may have a low affect as where fearful may have a high affect
        /// </summary>
        public int Affect { get; set; }
        /// <summary>
        /// Determines resulting function to use (Execute vs ExecuteOn)
        /// </summary>
        public bool IsTargeted { get; set; }
    }
}
