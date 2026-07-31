using System.Collections.Generic;
using EntityObjects.Interfaces;

namespace EntityObjects
{
    public class ActionCandidate
    {
        public IAction Action { get; set; }
        public Entity Target { get; set; }
        public List<ActionWeight> Weights { get; set; } = new List<ActionWeight>();

        public ActionCandidate(IAction action, Entity target)
        {
            Action = action;
            Target = target;
        }
    }

    public class ActionWeight
    {
        public int Weight { get; set; }
        public ActionWeightSource Source { get; set; }
    }

    public enum ActionWeightSource
    {
        Behaviors,
        Relationship,
        Status
    }
}
