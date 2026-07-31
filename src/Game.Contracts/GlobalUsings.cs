// The engine's item system is generic over the actor type; this game's
// actor is Entity. The alias keeps call sites reading as plain "IItem".
global using IItem = Engine.Items.IItem<Game.Contracts.Entity>;
