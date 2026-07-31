using EntityObjects;
using EntityObjects.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EntityObjects.EntityObjects
{
    /// <summary>
    /// Component that manages actions available to an entity
    /// Attach/detach actions dynamically based on entity type or behavior
    /// </summary>
    public class ActionModule
    {
        private Dictionary<string, IAction> _availableActions;

        public ActionModule()
        {
            _availableActions = new Dictionary<string, IAction>();
        }

        /// <summary>
        /// Attach an action to this entity
        /// </summary>
        public void AttachAction(IAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _availableActions[action.ActionName] = action;
        }

        /// <summary>
        /// Detach an action from this entity
        /// </summary>
        public bool DetachAction(string actionName)
        {
            return _availableActions.Remove(actionName);
        }

        /// <summary>
        /// Get an action by name
        /// </summary>
        public IAction? GetAction(string actionName)
        {
            _availableActions.TryGetValue(actionName, out var action);
            return action;
        }

        /// <summary>
        /// Get all available actions
        /// </summary>
        public IEnumerable<IAction> GetAllActions()
        {
            return _availableActions.Values;
        }

        /// <summary>
        /// Check if an action is available
        /// </summary>
        public bool HasAction(string actionName)
        {
            return _availableActions.ContainsKey(actionName);
        }
    }
}
