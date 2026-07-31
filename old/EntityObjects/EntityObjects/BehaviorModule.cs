using System;
using System.Collections.Generic;
using System.Text;

namespace EntityObjects.EntityObjects
{
    public class BehaviorModule
    {
        public string Name { get; set; }
        public string BehaviorType { get; set; }
        public int Affect { get; set; }
        public int Scalar { get; set; }

        public BehaviorModule(string name, string behaviorType, int affect, int scalar)
        {
            Name = name;
            BehaviorType = behaviorType;
            Affect = affect;
            Scalar = scalar;
        }
    }
}
